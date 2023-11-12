FROM mcr.microsoft.com/devcontainers/base:alpine-3.18

RUN apk add curl

RUN curl -fsSL https://dotnet.microsoft.com/download/dotnet/scripts/v1/dotnet-install.sh && chmod +x dotnet-install.sh

RUN ./dotnet-install.sh -c 8.0

RUN echo "export DOTNET_ROOT=/root/.dotnet" >> ~/.bashrc &&  echo "export PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools" >> ~/.bashrc
