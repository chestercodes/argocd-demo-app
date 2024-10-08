function GetValueOrFail($name){
    $v = [System.Environment]::GetEnvironmentVariable($name)
    if($null -eq $v){
        write-error "Cannot find $name"
        exit 1
    }

    return $v
}

$thehost = GetValueOrFail "DB_HOST"
$user = GetValueOrFail "DB_USERNAME"
$password = GetValueOrFail "DB_PASSWORD"
$database = GetValueOrFail "DB_DATABASE"

$connectionString = "Host=$thehost;Port=5432;User ID=$user;Password=$password;Database=$database"

dotnet tool install -g grate

grate --databasetype postgresql --connectionstring=$connectionString
exit $LASTEXITCODE