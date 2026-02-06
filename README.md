<div align="center">
  <h1 align="center">boorusky</h1>

  <p align="center">
    Booru bot for ATProtocol based sites
    <br />
    <a href="https://bsky.app/profile/yaoi-bot.bsky.social">View Demo</a>
    &middot;
    <a href="https://github.com/olifurz/boorusky/issues/new?labels=bug&template=bug-report---.md">Report Bug</a>
    &middot;
    <a href="https://github.com/olifurz/boorusky/issues/new?labels=enhancement&template=feature-request---.md">Request Feature</a>
  </p>
</div>

## About
Boorusky is a bot for [Bluesky](https://bsky.app/) and various other ATProtocol based sites. It scrapes every hour for 

## Getting Started
1. [Visit the releases](https://github.com/olifurz/boorusky/releases) and download the suitable executable for your system, or follow the instructions below to build for your system
2. You will need to create a `.env` file in the same place as this executable and setup your configuration, a template can be found [here](.env-template)
3. To test if the bot can successfully post, you can run the executable with the `-dry` argument

## Building
Instructions tested on an Arch Linux machine.
1. Make sure you have `dotnet` & `git` installed beforehand, see your package manager's documentation for more information
    ```
    dotnet --version
    ```
2. Clone the repository
    ```
    git clone https://github.com/olifurz/boorusky.git && cd boorusky
    ```
3.  Build the project, [read the docs](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-build) if you would like to target a specific OS, etc
    ```
    dotnet build
    ```
4. Navigate to /bin/Release/net9.0/[target] and run!

## License
Licensed under GNU General Public License v3.0, see [LICENSE](LICENSE) for more information
