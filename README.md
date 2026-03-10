# Launch Options
## API
### Using docker compose
In folder /backend with docker-compose.yml
```
docker compose up -d --build
```
Then call http://localhost:8080/import/importfromdata, it will load jsons from data folder to database.
### Manually
Run Postgres with **PostGIS** and change ConnectionString in /backend/Urban.API/appsettings.json

Run Urban.API in /backend/Urban.API
```
dotnet run
```

POSTGRES_USER is urbanuser, POSTGRES_PASSWORD is urbanpassword
## Frontend
In folder /frontend
```
npm install
npm run start
```
