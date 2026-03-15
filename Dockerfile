FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY TutoriaApi.sln .
COPY src/TutoriaApi.Core/TutoriaApi.Core.csproj src/TutoriaApi.Core/
COPY src/TutoriaApi.Infrastructure/TutoriaApi.Infrastructure.csproj src/TutoriaApi.Infrastructure/
COPY src/TutoriaApi.Web.API/TutoriaApi.Web.API.csproj src/TutoriaApi.Web.API/
COPY tests/TutoriaApi.Tests.Unit/TutoriaApi.Tests.Unit.csproj tests/TutoriaApi.Tests.Unit/
RUN dotnet restore TutoriaApi.sln

COPY . .
RUN dotnet publish src/TutoriaApi.Web.API/TutoriaApi.Web.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "TutoriaApi.Web.API.dll"]
