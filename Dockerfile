# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY EidCharity.slnx ./
COPY src/EidCharity.Domain/EidCharity.Domain.csproj src/EidCharity.Domain/
COPY src/EidCharity.Application/EidCharity.Application.csproj src/EidCharity.Application/
COPY src/EidCharity.Infrastructure/EidCharity.Infrastructure.csproj src/EidCharity.Infrastructure/
COPY src/EidCharity.Web/EidCharity.Web.csproj src/EidCharity.Web/
RUN dotnet restore src/EidCharity.Web/EidCharity.Web.csproj
COPY src/ src/
RUN dotnet publish src/EidCharity.Web/EidCharity.Web.csproj -c Release -o /app --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    TZ=Asia/Qatar
RUN mkdir -p /app/data/files /app/data/keys && chown -R app:app /app/data
COPY --from=build /app .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "EidCharity.Web.dll"]
