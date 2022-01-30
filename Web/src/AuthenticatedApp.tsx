import { Fragment } from 'react';
import { Navigate } from 'react-router-dom';
import { useLocation, useSearchParams } from 'react-router-dom';
import { useAuthContext } from './AuthContext';
import { getHostUrl, useApiCall } from './helpers';
import { shouldShowGooglePlayBadge } from './MobileCheck';

const clientId = `${process.env.REACT_APP_STRAVA_API_CLIENT_ID}`;
const authenticatedPath = '/authenticated';
const requiredScope = 'read,activity:read'.split(',').sort();
const authenticatedUri = `${getHostUrl()}${authenticatedPath}`;
const loginUrl = `https://www.strava.com/oauth/authorize?client_id=${clientId}&redirect_uri=${authenticatedUri}&response_type=code&approval_prompt=auto&scope=${requiredScope},activity:read_all`;

export default function AuthenticatedApp({ children }) {
    const location = useLocation();
    const [query,] = useSearchParams();
    const authContext = useAuthContext();
    
    // called back from the Strava authentication?
    if (location.pathname === authenticatedPath) {
        const scope = query.get('scope').split(',').sort();
        if (!requiredScope.every(s => scope.includes(s))) {
            return <MissingScopes />;
        }

        const code = query.get('code');
        return <Login code={code} />;
    }

    // never logged in before or session expired?
    if (!authContext.athleteId) {
        return <Redirecting />;
    }

    // authenticated
    return children;
}

function MissingScopes() {
    return (
        <div>
            <p>Missing scopes! In order to use this app, you have to grant the access to read activities.</p>
            <p><a href={loginUrl}>Authenticate again</a></p>
        </div>
    );
}

function Redirecting() {
    if (shouldShowGooglePlayBadge()) {
        return (
            <div>
                <p>To authenticate with Strava, click the <a href={loginUrl}>link</a></p>
            </div>
        );
    } else {
        window.location.assign(loginUrl);
        return (
            <div>
                <p>Redirecting to Strava to authenticate...</p>
                <p>If you were not redirected automatically, click the <a href={loginUrl}>link</a></p>
            </div>
        );
    }
}

function Login({ code }) {
    const authContext = useAuthContext();
    const state = useApiCall(() => authContext.login(code));
    
    return (
        <Fragment>
            {
                (state.loading && <span>Authenticating...</span>)
                || (state.errorMessage && <LoginError errorMessage={state.errorMessage} />)
                || <Navigate to='/' />
            }
        </Fragment>
    );
}

function LoginError({ errorMessage }) {
    return (
        <div>
            <p>Authentication error: {errorMessage}</p>
            <p><a href={loginUrl}>Authenticate again</a></p>
        </div>
    );
}
