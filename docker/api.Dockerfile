FROM mcr.microsoft.com/dotnet/sdk:8.0 as base
COPY api /src/api
WORKDIR /src/api
RUN dotnet publish -c Release -o /out
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled
COPY --from=base /out .
ENTRYPOINT [ "./api" ]