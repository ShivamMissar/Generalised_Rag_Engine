# Builds the API (src/Rag.Api) into a small container image for hosting, e.g. on Render (see render.yaml).

# 1. Build with the full .NET SDK.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ src/
RUN dotnet publish src/Rag.Api/Rag.Api.csproj -c Release -o /app/publish

# 2. Run with just the ASP.NET Core runtime, as a non-root user.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
# The fictional sample CV behind "Try the sample CV".
COPY samples/candidate/ samples/candidate/
ENV ASPNETCORE_ENVIRONMENT=Production \
    Rag__SamplesPath=/app/samples/candidate
USER $APP_UID
EXPOSE 8080
# Hosts like Render say which port to listen on through $PORT; default to 8080 otherwise.
ENTRYPOINT ["sh", "-c", "ASPNETCORE_HTTP_PORTS=${PORT:-8080} exec dotnet Rag.Api.dll"]
