Write-Host 'Starting the MongoDb backup'

Write-Host 'Getting the DB host from config...'
$settings = Get-Content -Raw '../Sync/local.settings.overrides.json' | ConvertFrom-Json -AsHashTable
$dbConnectionString = $settings['Values']['DocumentDBConnection']

Write-Host 'Creating a new target directory...'
$now = Get-Date (Get-Date).ToUniversalTime() -UFormat '+%Y-%m-%dT%H.%M.%S.000Z'
$backupDir = "backup-$now"
$backupDirPath = Join-Path -Path $PWD -ChildPath $backupDir
New-Item -Path "$backupDirPath" -ItemType Directory

Write-Host 'Exporting the DB to the target directory...'
# https://stackoverflow.com/a/16334189
& cmd /c ('mongoexport --uri="{0}" --db=anychallenge --collection=activities --out="{1}/activities.json" 2>&1' -f $dbConnectionString,$backupDir)
& cmd /c ('mongoexport --uri="{0}" --db=anychallenge --collection=athletes --out="{1}/athletes.json" 2>&1' -f $dbConnectionString,$backupDir)
& cmd /c ('mongoexport --uri="{0}" --db=anychallenge --collection=challenges --out="{1}/challenges.json" 2>&1' -f $dbConnectionString,$backupDir)

Write-Host 'Zipping the exported content and remove the target directory...'
Compress-Archive -Path $backupDir -DestinationPath "$backupDir.zip"
Remove-Item -Path $backupDirPath -Recurse

Write-Host 'Done'
