# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY FinanceTracker.sln ./
COPY Shared/Shared.csproj Shared/
COPY Domain/Domain.csproj Domain/
COPY Application/Application.csproj Application/
COPY Persistence/Persistence.csproj Persistence/
COPY Identity/Identity.csproj Identity/
COPY Services/Services.csproj Services/
COPY Presentation/Presentation.csproj Presentation/

RUN dotnet restore Presentation/Presentation.csproj

COPY . .
RUN dotnet publish Presentation/Presentation.csproj -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Presentation.dll"]
