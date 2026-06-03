# ───────────── Build stage ─────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["src/Nexus.Domain/Nexus.Domain.csproj", "src/Nexus.Domain/"]
COPY ["src/Nexus.Application/Nexus.Application.csproj", "src/Nexus.Application/"]
COPY ["src/Nexus.Infrastructure/Nexus.Infrastructure.csproj", "src/Nexus.Infrastructure/"]
COPY ["src/Nexus.Web/Nexus.Web.csproj", "src/Nexus.Web/"]
RUN dotnet restore "src/Nexus.Web/Nexus.Web.csproj"

COPY . .
RUN dotnet publish "src/Nexus.Web/Nexus.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ───────────── Runtime stage ─────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# DataProtection keys: caminho dentro de /home/app que JÁ pertence ao user app
# da imagem oficial. Cria o diretório com as permissões certas no build, assim
# quando o volume Docker for criado pela primeira vez ele herda dono app:app.
USER root
RUN mkdir -p /home/app/dpkeys && chown -R app:app /home/app/dpkeys
USER app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Nexus.Web.dll"]
