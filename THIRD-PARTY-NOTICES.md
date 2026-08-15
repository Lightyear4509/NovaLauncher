# Third-party notices

NovaLauncher runtime distributions include components from the following
projects. Exact versions and the complete transitive graph are recorded in the
committed NuGet lock files and the release CycloneDX SBOM.

- Avalonia UI 12.1.1 — MIT
- Microsoft .NET runtime and Microsoft.Extensions 10.0.10 — MIT
- SkiaSharp 3.119.4 — MIT
- HarfBuzzSharp 8.3.1.3 — MIT
- MicroCom.Runtime 0.11.6 — MIT
- Tmds.DBus.Protocol 0.94.1 — MIT
- Avalonia ANGLE Windows native assets 2.1.27548.20260419 — BSD-style license reproduced below

Build and test dependencies such as xUnit, Coverlet, and Microsoft.NET.Test.Sdk
are not shipped as application runtime files. The Windows setup executable is
built with Inno Setup 7.1.0; Inno Setup itself is not redistributed as part of
NovaLauncher. See https://jrsoftware.org/isinfo.php.

## ANGLE license

Copyright 2018 The ANGLE Project Authors. All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.
3. Neither the name of TransGaming Inc., Google Inc., 3DLabs Inc. Ltd., nor the
   names of their contributors may be used to endorse or promote products
   derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
