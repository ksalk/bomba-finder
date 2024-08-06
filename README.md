# bomba-finder
Tool for identifying the specific episode of Kapitan Bomba where a given dialogue appears

# usage

To generate SQLite DB with subtitles for Kapitan Bomba, run **youtube-sub-scraper**. Go it's directory and run:

```
dotnet build
dotnet run
```

File called **bomba.db** will be created inside _/bin/debug/net8.0_ directory.

You can copy this file to **bomba-bot** directory. To run bot, you must create **.env** file in this directory as well and fill it with your discord token:

```
DISCORD_TOKEN=your_token
```

Then you can run and stop the bot using bash script:

```
./bomba start
./bomba stop
```