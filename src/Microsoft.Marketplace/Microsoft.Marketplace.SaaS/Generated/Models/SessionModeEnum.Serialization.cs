// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

using System;

namespace Microsoft.Marketplace.SaaS.Models
{
    internal static partial class SessionModeEnumExtensions
    {
        public static string ToSerialString(this SessionModeEnum value) => value switch
        {
            SessionModeEnum.None => "None",
            SessionModeEnum.DryRun => "DryRun",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown SessionModeEnum value.")
        };

        public static SessionModeEnum ToSessionModeEnum(this string value)
        {
            if (string.Equals(value, "None", StringComparison.InvariantCultureIgnoreCase)) return SessionModeEnum.None;
            if (string.Equals(value, "DryRun", StringComparison.InvariantCultureIgnoreCase)) return SessionModeEnum.DryRun;
            throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown SessionModeEnum value.");
        }
    }
}
