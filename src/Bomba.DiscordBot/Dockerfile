# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY Bomba.sln ./
COPY src/Bomba.DiscordBot/Bomba.DiscordBot.csproj ./src/Bomba.DiscordBot/
COPY src/Bomba.DB/Bomba.DB.csproj ./src/Bomba.DB/
COPY src/Bomba.Querying/Bomba.Querying.csproj ./src/Bomba.Querying/
COPY src/Bomba.Embeddings/Bomba.Embeddings.csproj ./src/Bomba.Embeddings/

# Restore dependencies
RUN dotnet restore src/Bomba.DiscordBot/Bomba.DiscordBot.csproj

# Copy source code
COPY src/ ./src/

# Build and publish
RUN dotnet publish src/Bomba.DiscordBot/Bomba.DiscordBot.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Run as non-root user for security
RUN useradd -m -s /bin/bash appuser
USER appuser

ENTRYPOINT ["dotnet", "Bomba.DiscordBot.dll"]
