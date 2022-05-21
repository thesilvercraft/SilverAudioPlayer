﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

internal partial class Interop
{
    [DllImport(LibcLibrary, SetLastError = true)]
    internal static extern int ioctl(int fd, uint request, IntPtr argp);

    [DllImport(LibcLibrary, SetLastError = true)]
    internal static extern int ioctl(int fd, int request, IntPtr argp);

    [DllImport(LibcLibrary, SetLastError = true)]
    internal static extern int ioctl(int fd, uint request, ulong argp);
}