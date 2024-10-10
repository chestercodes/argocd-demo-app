FROM erikbra/grate:latest as base
WORKDIR /app
COPY migrations/up ./up
ENV DB_HOST=change_me
ENV DB_USERNAME=change_me
ENV DB_PASSWORD=change_me
ENV DB_DATABASE=change_me

ENTRYPOINT ./grate \
            --connstring="Host=$DB_HOST;Port=5432;User ID=$DB_USERNAME;Password=$DB_PASSWORD;Database=$DB_DATABASE" \
            --databasetype=postgresql