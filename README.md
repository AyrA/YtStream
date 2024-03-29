# YtStream
Live Youtube to MP3 transcoder

This is a much more advanced version of [php-ytstream](https://github.com/AyrA/php-ytstream)
written in ASP.NET Core using .NET 6.

## Terms and abbreviations used in this document

- YT: YouTube
- SBlock: SponsorBlock

## Notable features

This application is a lot more than a simple MP3 downloader

### Live streaming

Data is sent to the client and to the cache (if enabled) at the same time as it's downloaded and converted.
Because of that, streaming will start as soon as the first MP3 fragment (approx 20-30 ms) arrives.

### Live stream speed simulation

Data is normally sent to the client as fast as possible.
While this is great for downloading, it's not so great for streaming.
It can overwhelm webradio devices, or it can cause web browsers to cache so much audio
that they don't request new data for a long time, causing the server to close the connection.

YtStream supports a setting that delivers data with the exact speed needed to play it back,
with an added buffer of a few seconds.

### Removal of non-music sections

By using SBlock, non-music sections that have been marked as such by the community will be automatically removed.
The section is only removed from the file that's streamed to the client and not the cached copy.
This ensures that changes to the SBlock ranges can be accounted for without having to download the file again.

This application only removes section marked as "non-music".
It will not remove other sections such as end cards because this tends to cut off the music.

### Stream concatenation

Users can concatenate multiple ids into a single audio file.
The resulting file is joined in a seamless manner
and doesn't requires any special streaming capabilities by the user.

### Playlist support

This application supports playback of entire youtube playlists.
Playlist ids and video ids can be mixed together.

### Fully configurable from your browser

All settings can be fully accessed and changed from your browser.
No manual editing of your configuration is necessary.

### Restrictions for streaming

You can restrict streaming to registered users only.
You can also disable registration, creating a private streaming platform.

### Intermissions

Custom intermissions can be inserted before the first file, between files, and after the last file.

This can be used for ads or other purposes.

### Media Player

Instead of directly streaming data, YtStream offers a fully functional media player
that allows you to play any public YT playlist as if they were local files.
Note however that due to the streaming nature of this application, seeking around the files is not supported.

## Additional licenses

SBlock operates on a custom license that requires attribution.

[See here for licensing details](https://github.com/ajayyy/SponsorBlock/wiki/Database-and-API-License)

The gist of it is that you cannot run YtStream as a commercial service under this license.
However, the license only applies if you use SBlock.

## The AGPL

YtStream itself uses the AGPL which considers SaaS/hosting a form of publishing.
You must provide unobstructed and obvious access to the YtStream source
including all modifications you made.

For the exact details, see the `LICENSE` file.

## Installation

1. [Download](https://gitload.net/AyrA/YtStream)
2. Extract
3. Edit the base path in `appsettings.json` and set it to an empty directory
4. Run `YtStream.exe`
5. Go to http://localhost:5000

YtStream is built in a way that allows it to function in one of three ways:

- Standalone (as shown above) which also works as reverse proxy backend for apache, nginx or other web servers
- Using as IIS module with the provided web.config file
- As Windows Service (this supposedly also supports Linux systemd)

Note: Commits are generally only pushed to master after building them locally.
This means you should be able to download and build
the latest version of this project from source as well.

### File system layout

YtStream creates the following files and folders in the specified base path directory:

| Name          | Type   | Description                                                  |
|---------------|--------|--------------------------------------------------------------|
| Cache         | Folder | Holds cached files. Can be freely moved or disabled entirely |
| accounts.json | File   | Holds user accounts and their favorites                      |
| config.json   | File   | Holds application settings                                   |
| ads.json      | File   | Holds the intermission configuration                         |

The cache location can be changed in the settings. This will not move existing cached content.

The JSON files are only written to when you make changes to the settings, user accounts,
or intermissions.

**CAUTION!** Do not directly edit the JSON files while the application is running.
The changes may not be reflected in the application, and they may be overwritten.
There is nothing in the settings or account file you cannot configure in the application itself.
If you do invalid edits the application may refuse to start,
or it may force itself into lockdown mode.

### Dependencies

This application requires FFmpeg and Youtube-Dl (or a fork) to work.
You can install them anywhere you like and then specify the path to the tools in the settings.
The settings page has links to the appropriate download pages of said tools.

Note: The application will start and run without these tools,
but streaming functionality will be unavailable until said tools are configured.

### Changing listener

You can supply the command line argument `--urls` followed by one or more URLs to listen to.

Examples:

- `--urls "http://localhost:5000"` Identical to the default configuration
- `--urls "http://127.0.0.1:54321"` Listen on the IPv4 loopback interface on port 54321
- `--urls "http://127.0.0.1:54321;http://[::1]:54321"` Same as above but also listen for IPv6
- `--urls "http://0.0.0.0:80;http://[::]:80"` Listen on all interfaces (IPv4 and IPv6) on port 80

Note: Linux likely requires administrative permissions for listening on a port less than 1024

You can also listen for TLS connections, dynamic ports, and unix sockets.
[See details here](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints)

This type of application is normally run behind a reverse proxy.
There is nothing that makes it inherently unsafe to directly run it on port 80 or 443 though.

Apache and Nginx are popular web servers that support reverse proxying on Linux.
Apache also runs on Windows, but IIS may be preferred there.

## Initial configuration

The application will shout at you on the home page until you configure it properly.
It's in a locked state until configured, meaning that streaming functionality is unavailable.

### Creating the first account

The login form will redirect you to the registration form as long as no accounts exist.
Registration of the first account is always allowed regardless of your settings.
The first account will also automatically have the "Administrator" role assigned.

## Changing settings

The application already has sensible defaults set.
You likely only need to configure the dependencies and are ready to go.

# Administrative features

An administrator can manage various features of the application

## Settings

You can change the follow settings:

### Conversion tools

- Set the path of FFmpeg and Youtube-Dl

### Youtube

- Set the YouTube API key. Optional for streaming, required for the url builder or the built-in media player

### Cache

- Disable the cache completely
- Set cache base folder
- Set lifetime for MP3 file cache

### SBlock

- Disable SBlock entirely
- Set a custom SBlock server
- Set lifetime of SBlock ranges

### Streaming options

- Disable anonymous streaming
- Enable marking of ads in MP3 files using the "private" bit
- Configure ads for administrators
- Limit number of video ids users can concatenate
- Limit permitted runtime of a single video file
- Set audio bitrate and sampling frequency

**CAUTION!** Read the warning below the bitrate and frequency settings before changing them.

### Accounts

- Enable public registration
- Set maximum number of API keys per user

## Application lock

You can lock and unlock the application at any time.
When locked, no new streaming sessions can be started.
Other features such as management, login and registration continue to function normally.

Streaming sessions that are already running will exit after the current file finishes processing.

The application starts unlocked by default.
It's automatically locked if there's a problem with the configuration,
and it will not permit unlocking until the issue is fixed.

## Accounts

Accounts can be added, edited, deleted, enabled and disabled.

Note: It's not possible to modify the currently logged in account.
Editing the current account can be done by clicking on the user name in the top right instead.

Actions that would make unusable or remove the last active administrator account are not permitted.

### Additional options

Administrators can disable ads for a given account.

### Types

There exist two types of accounts:

#### User

This is the default type. It has no special abilities.

#### Administrator

This type allows the user to manage the application.
Whenever this type is added to an account, the type "User" is also added.

### Disabled accounts

A disabled account cannot log in or stream content.
If disabled, API keys of this user are also disabled.

### Effect on sessions

Editing accounts has an effect on open sessions.
Changing the name or disabling the account will log out any active session
the next time the user makes a request to this server.

## Cache

The MP3 file cache can be inspected and purged.
You can either delete only expired files (if expiration is enabled that is),
or delete all files unconditionally.
Purging the cache is useful when you change the audio bitrate or sample frequency settings.

A tool to convert between youtube video ids and cache file ids is provided too.

## Backup

You can export settings and accounts as well as import them.

Importing only works if the data is in a valid state.
You can't import an account list that has no active administrators for example.

## Ads

As an administrator you can configure this application to inject ads into streams.

Any number of ads can be uploaded and will be automatically converted into MP3.

A processed ad can be configured for one or more of the following categories:

- **Intro**: Plays before the first file starts streaming
- **Intermission**: Plays between two streams
- **Outro**: Plays after the last file ends streaming

If multiple ads are configured for the same category,
one will be picked at random every time an ad is requested from the system.

The system doesn't counts how often an ad has been played,
and it doesn't ensures that all ads are played equal amounts.

### Uploading

You currently cannot rename ad files.
Be sure to name your file properly before uploading it.
If a name conflict happens, a counter will be added to the current file name.
The upload limit is 50 MB per request.
Within the request limit you can upload as many files at once as you want to.
The file dialog supports multi select of files.

### Audio format

Ads can be in any format that FFmpeg understands (most video and audio formats).
FFmpeg will pick the first (or primary) audio stream and convert that to MP3.
Other information such as video channels or metadata is discarded.

The ad is only converted if it's not in the configured MP3 format.
An ad that is already an MP3 with the same bitrate and frequency as you configured,
will simply be filtered and copied to the cache without using FFmpeg.

### Adding and removing

An ad can be added to the respective category via the drop down at the end of the list.
Ads can be removed again by clicking the "Remove" button.
Removing an ad from all categories will not delete the physical file.

### Deleting

Deleting an ad will automatically remove it from all categories too.

### Ad blocking

Ads are transparently injected into the stream and are therefore unblockable.

If you're required to somehow make them detectable,
you can enable a setting that marks ads using the "private" bit in the MP3 header.
This would allow a machine to detect whether an audio block belongs to an ad or not.
