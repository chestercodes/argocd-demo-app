FROM mcr.microsoft.com/dotnet/sdk:8.0
COPY migrations /src/migrations
WORKDIR /src/migrations
CMD pwsh run.ps1
