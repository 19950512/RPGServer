# Etapa de build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia o csproj e restaura dependências
COPY ["RPGServer/RPGServer.csproj", "RPGServer/"]
RUN dotnet restore "RPGServer/RPGServer.csproj"

# Copia o restante do código e publica
COPY . .
WORKDIR "/src/RPGServer"
RUN dotnet publish "RPGServer.csproj" -c Release -o /app/publish

# Etapa final (runtime)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5000
EXPOSE 5001
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RPGServer.dll"]
