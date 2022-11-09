FROM mcr.microsoft.com/dotnet/sdk:6.0
RUN apt update && apt install -y sudo
RUN useradd -m dotnet
RUN adduser dotnet sudo
RUN echo '%sudo ALL=(ALL) NOPASSWD:ALL' >> /etc/sudoers
USER dotnet
