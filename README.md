# YtStream
Live Youtube to MP3 transcoder

This is a much more advanced version of [php-ytstream](https://github.com/AyrA/php-ytstream)
written in ASP.NET Core.

## Terms and abbreviations used in this document

- YT: YouTube
- SBlock: SponsorBlock

## Notable features

- Live streaming of YT content. Data is sent while it's downloaded to the server
- Automatic trimming of non-music sections using SBlock
- User can concatenate multiple ids into a single audio file
- Supports playback of entire youtube playlists
- Fully configurable (cache duration, audio quality, etc)
- Restrictions for streaming
- Insertion of custom intermissions (Ads. It's ads)
- Can work on readonly file systems (with limitations)

## Additional licenses

SBlock operates on a custom license that requires attribution.

[See here for licensing details](https://github.com/ajayyy/SponsorBlock/wiki/Database-and-API-License)

You cannot run YtStream this as a commercial service under this license.

Note that YtStream uses the AGPL which considers SaaS/hosting a form of publishing

## Installation

1. [Download](https://gitload.net/AyrA/YtStream)
2. Extract
3. Run `YtStream.exe`
4. Go to http://localhost:5000

Note: Commits are generally only pushed to master after testing them.
This means you should be able to download and build this project from source as well.

### File system layout

YtStream creates the following files and folders in the application directory:

| Name          | Type   | Description                                                  |
|---------------|--------|--------------------------------------------------------------|
| Cache         | Folder | Holds cached files. Can be freely moved or disabled entirely |
| accounts.json | File   | Holds user accounts                                          |
| config.json   | File   | Holds application settings                                   |

The cache location can be changed in the settings. This will not move existing cached content.

The JSON files are only written to when you make changes to the settings or user accounts.
The application can run on a readonly file system as long as you don't do that.

**CAUTION!** Do not directly edit the JSON files while the application is running.
The changes may not be reflected in the application, and they may be overwritten.
There is nothing in the settings or account file you cannot configure in the application itself.
If you do invalid edits the application may refuse to start.

### Dependencies

This application requires FFmpeg and Youtube-Dl to work.
You can install them anywhere you like and then specify the path to the tools in the settings.

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
There is nothing that makes it inherently unsafe to directly run it on port 80 or 443.

Apache and Nginx are popular web servers that support reverse proxying.
Apache also runs on Windows.

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
- Configure ads for administrators
- Set audio bitrate and sampling frequency

**CAUTION!** Read the warning below the bitrate and frequency settings.

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
Editing the current account can be done by clicking on the user name in the top right.

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
You can either delete expired files only (if expiration is enabled that is),
or delete all files unconditionally.
Purging the cache is useful when you change the audio bitrate or sample frequency settings.

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

### Uploading

You currently cannot rename ad files.
Be sure to name your file properly before uploading it.
If a name conflict happens, a counter will be added to the current file name.
The upload limit is 50 MB per request.
You can upload as many files at once as you want to.
The dialog supports multi select of files.

### Audio format

Ads can be in any audio format that FFmpeg understands.
The ad is only converted if it's not in the configured audio format.
An ad that is already an MP3 with the same bitrate and frequency as you configured,
it will simply be filtered and copied to the cache without using FFmpeg.

### Adding and removing

An ad can be added to the respective category via the drop down at the end of the list.
Ads can be removed again by clicking the "Remove" button.
Removing an ad from all categories will not delete it.

### Deleting

Deleting an ad will automatically remove it from all categories too.

### Ad blocking

Ads are transparently injected into the stream and are therefore unblockable.
