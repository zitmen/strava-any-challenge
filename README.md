About
=====
Any Challenge is a simple web/mobile application for fitness challenges. You can set up a challenge with duration up to 1 month and compete with your friends in challenges like total distance, total calories burned, total moving time, etc.

The app has been created as a weekend project because there was no possibility to create such challenges in Garmin nor in Strava and the existing apps I found were either somehow limited or required a paid subscription. Thus, I decided to write my own and to use it also to test out serverless computing via Azure Functions and to try Android development using Xamarin.

How it works is that first the users need to log in to the application using their Strava account. Then they can join a challenge created by the adminitrator. When the users complete activities and synchronize their devices with Strava, the Strava backend publishes an event, which the Any Challenge app is subscribed to via a Webhook. When the event hits the Any Challenge backend, we store it to a database, then we propagate the event to a function, which aggregates activities into challenges and we update challenge leaderboard. The users can see the leaderboard using a web application which can be accessed via a web browser (desktop or mobile) or using an Android app.

![Screenshot: List of challenges](./docs/images/app-list.png?raw=true "Screenshot: List of challenges")
![Screenshot: Challenge detail](./docs/images/app-challenge.png?raw=true "Screenshot: Challenge detail")

Implementation
==============
There are four components:
- synchronization engine built from Azure Functions and Azure Event Grid, which accepts activities from Strava via a webhook, stores them to a document database (MongoDB), agregates the data into challenge summaries and stores the summaries to the database
- REST API hosted as Azure Web App, which simply reads the aggregated data from the database
- React.JS hosted as Azure Static Web App, which provides a user-friendly web-based interface
- Android application writen in Xamarin distributed via a private Google Play alpha channel; the app is practically just a `WebView`, which hosts the React web app

![Diagram of the architecture](./docs/images/architecture.png?raw=true "Diagram of the architecture")

Note that the app is hosted in free tier of Azure. Azure Functions, Event Grid and Web Apps are all provided by Microsoft for free.
The MongoDB cluster is hosted in Azure as well, but I created it via Mongo Atlas, which provides a cluster of three DB servers in certain Azure regions for free.
The Strava APIs are for free as well. But since I use the free tier, there is a relatively strict limit on the number of API requests, which is why I will not open the application to the public. Instead, you can fork it, deploy it yourself, register it as an API application in your Strava account and share it with your circle of friends.

There is a couple of technical issues to note here:
- Since the app is not used by many people, I had to add the `KeepAlive` function triggered every 10 minutes to keep the Azure Functions in the synchronization chain warmed up. Otherwise, the host would get automatically decomissioned and the warm up would be triggered by the Strava webhook. Since the warm up is slow, the webhook would time out and fail.
- The Azure Functions triggered by the EventGrid are not using the EventGrid triggers built in Azure Functions, because I had issues with timeouts and incorrect routing of events when using it. The events were getting randomly dropped. I am not sure if it is because of the free tier or just some issue in the EventGrid trigger, but I found that rewriting the code to use REST API trigger instead solves the issue and the chain processing is stable now.
- REST API calls are authenticated via a custom header because if the standard Auth header is used, it is interfering with the Azure Web App authentication and it does not work. Maybe I have to configure it somewhere, but I got lazy and found this easier than to spend more time trying to find anything realted to this in the documentation or on forums.

Deployment
==========
Here is just a couple of pointers, that should help with deployment to a new environment.

The app is deployed into Azure via Github Actions. The MongoDB cluster and the Android App are not set up by the CD pipeline. In the MongoDB, you have to manually create collections `athletes`, `activities`, `challenges` and `sync`.

In the source code repository, the `Api` and `Sync` projects have files `local.settings.json` which contain a list of environment variables you either have to set up in your Azure environment, or you have create a file `local.settings.overrides.json` and define the variables there. The `Web` project uses `.env` file to configure the Strava client ID. It has to be overridden either by defining the environment variable or by adding it to a `.env.local`. In the `Android` project, there is a `Config` class in the `Config.cs` and it contains environment-specific variables. The class is `partial` so you can fill in the missing values in an git-ignored file `Config.overrides.cs`.

If there is an issue with publishing Azure Functions, make sure `AzureWebJobsStorage` is set in the Function Application settings in Azure.

To register the [Strava webhook](https://developers.strava.com/docs/webhooks/) run this:
```
curl --location --request POST 'https://www.strava.com/api/v3/push_subscriptions' \
--header 'Content-Type: application/json' \
--data-raw '{
    "client_id": {YOUR_STRAVA_APP_CLIENT_ID},
    "client_secret": "{YOUR_STRAVA_APP_SECRET}",
    "callback_url": "https://{YOUR_API_HOST_NAME}.azurewebsites.net/api/Webhook",
    "verify_token": "{YOUR_STRAVA_VERIFICATION_TOKEN}"
}'
```

Debugging
=========
The Azure Functions and the REST API can be built and run using the dotnet commands as any other dotnet app.
The Web app can be started locally using the Node.JS dev server using `npm` or `yarn` commands.
The Android App can be debugged either using an Android emulator (available for example via Visual Studio) or in your Android device over USB.

To modify the configuration of projects, look at the Deployment section as the same apply for local debugging.

In order to debug the Strava Webhooks, you must have a public IP or create a tunnel so it can be accessed by the API calls from Strava.com. I used the [ngrok](https://ngrok.com/) to create the tunnel for free.

MongoDB can be either created for free online in Mongo Atlas or it can be installed locally to the system or started in a Docker container.

Known issues
============
Since this is just a "weekend" project, it is not as optimized and not as resilient as you would expect from a commertial application running in production. Although, we started using this with my friends in February 2022 and it runs with no maintenance and no major issues, which could not be fixed by a manually triggered sync.

Some issues, which could occur are for example partial sync failure due to auth, throttle or random network issues. But these are usually corrected automatically when the next event is processed.
