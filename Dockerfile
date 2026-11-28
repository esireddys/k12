# The K12 Practice API with the web app inside it: one container, one address (see api/src/K12.Api/Hosting).
# Build from the repository root:  docker build -t k12-api .

# 1. The React app. VITE_API_URL is empty, so the app calls the API on its own address.
FROM node:22-alpine AS web
WORKDIR /src/web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
ENV VITE_API_URL=""
RUN npm run build

# 2. The API.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src/api
COPY api/ ./
RUN dotnet publish src/K12.Api -c Release -o /app

# 3. Runtime: ASP.NET Core only, as the image's non-root user, listening on 8080.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /src/web/dist ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "K12.Api.dll"]
