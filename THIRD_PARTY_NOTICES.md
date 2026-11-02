# Third-party notices

## TailAdmin Next.js Free

CoachHub uses the visual design and may adapt selected MIT-licensed source or assets from:

- Project: TailAdmin Next.js Free
- Version inspected: 2.3.0
- Copyright: Copyright (c) 2023 TailAdmin
- License: MIT
- Local reference supplied for this project: W:\Work\GYM\free-nextjs-admin-dashboard-main
- Upstream project identified by the supplied README: https://github.com/TailAdmin/free-nextjs-admin-dashboard

The full license text is included at third-party/TailAdmin-LICENSE.

## SkiaSharp

CoachHub's API decodes and re-encodes uploaded images (P20: metadata removal, orientation, size cap) with:

- Packages: `SkiaSharp` and `SkiaSharp.NativeAssets.Linux.NoDependencies`
- Version: 4.153.1
- Copyright: Copyright (c) 2015-2016 Xamarin, Inc.; Copyright (c) 2017-2018 Microsoft Corporation
- License: MIT (SkiaSharp); the bundled native Skia library is under the BSD-3-Clause license of the Skia project
- Upstream project: https://github.com/mono/SkiaSharp

The Linux package carries the native library without a fontconfig dependency, because the processor never draws text. Windows and macOS natives come with `SkiaSharp` itself.
