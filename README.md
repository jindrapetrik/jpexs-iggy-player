# JPEXS Iggy Player

A standalone Windows x64 player for 64-bit IGGY movies, using the supported
Iggy 1.2.30 or 1.2.56 DLL. Requires .NET Framework 4.x.

## Setup and running

Place the runtime DLL in `lib/` beside the executable. Lookup order:

1. `lib/iggy_w64_final.dll`
2. `lib/iggy_w64.dll` (only if the first file is absent)

Only the two supported binaries are compatible.
DLLs are not part of the distribution. (You can find them on the internet.)

```powershell
.\IggyPlayer.exe "test-data\graphics.iggy"
```

You can also use **File > Open**, drag a file into the window.
Playback controls provide pause, seeking, reload and mute;
**File > Open log** shows diagnostics.

OpenGL is selected automatically, with software rendering as a fallback.
Use `--renderer auto|opengl|software` to select a backend, or `--dll PATH`
to override DLL lookup.

Keep imported IGGY libraries and companion `.raw.iggytex` / `.iggytex`
textures beside their movies. Local saved data is stored in `sharedobjects/`
and saved automatically when closing or replacing a movie.

AS3 `URLLoader` reads local files and supports HTTP/HTTPS GET and POST with
request data and custom headers. Movies have desktop network access;
Flash's cross-domain policy sandbox is not emulated.

## Building and testing

```powershell
.\build.ps1
.\test.ps1
.\test.ps1 -TestOpenGl -TestAudio
```

Building uses the C# compiler included with Windows .NET Framework and the
checked-in `runtime/*.abc` resources. OpenGL/audio tests require suitable
hardware and produce audible tones. Results are written to `test-output/`.
See [test-data/README.md](test-data/README.md) for fixture details.

## Limitations

32-bit IGGY and direct SWF playback are unsupported. Network loading
through display `Loader`, video, streaming audio and some AS3 APIs are not
implemented. Rendering and seeking can differ from Flash Player.
Source movies and DLLs are not modified.

## License

Player source: [MIT License](LICENSE.md). The BC7 decoder retains its license
notice in [src/Bc7Decoder.cs](src/Bc7Decoder.cs). Proprietary Iggy runtime DLLs
are not covered by this license.

## About

Author: Jindra Petřík (JPEXS).

The player was made using high usage of AI (Open AI Codex).
