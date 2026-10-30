FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TeeTimeFinder.slnx ./
COPY Api/TeeTimeFinder.Api.csproj Api/
COPY Client/TeeTimeFinder.Client.csproj Client/
COPY Core/TeeTimeFinder.Core.csproj Core/
RUN dotnet restore

COPY . .
WORKDIR /src/Api
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TeeTimeFinder.Api.dll"]
