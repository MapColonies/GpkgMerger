#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
ARG VERSION="9.9.9.9"
WORKDIR /src
COPY ["MergerService/MergerService.csproj", "MergerService/"]
COPY ["MergerLogic/MergerLogic.csproj", "MergerLogic/"]
RUN dotnet restore "MergerService/MergerService.csproj"
COPY . .
WORKDIR "/src/MergerService"
RUN dotnet build "MergerService.csproj" -c Release -o /app/build /p:Version=$VERSION

FROM build AS publish
RUN dotnet publish "MergerService.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MergerService.dll"]
