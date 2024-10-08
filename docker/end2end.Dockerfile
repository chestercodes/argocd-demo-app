FROM mcr.microsoft.com/dotnet/sdk:8.0
COPY end2end /src/end2end
WORKDIR /src/end2end
CMD dotnet test
