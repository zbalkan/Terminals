# Terminals.External consumer and compatibility audit

This document records the source-level dependency contracts of `Terminals.External` for the 5.0.0 modernization. It is an evidence record, **not a second roadmap or status tracker**. The consolidated work matrix remains in [Dependency-modernization-roadmap.md](Dependency-modernization-roadmap.md).

**Historical baseline:** `master` `dd9871feea5bf51afaadd5f66f0f487b811c654d` (November 11, 2026). The inspection covered `Terminals.External.csproj`, its compiled source, and first-party C# reference checks, including generated WinForms designer files. No build or runtime validation was performed for this audit.

## Confirmed first-party consumers

| Component | Source-level consumer or contract | Conclusion |
|---|---|---|
| Command-line parser (`CommandLineArguments.cs`) | `Source/Terminals/Program.cs` uses `Parser.ParseArguments` for startup options; `CommandLineArgs` is forwarded through the named-pipe command service | Retain parser and its argument attributes; a parser replacement would need CLI compatibility tests |
| `IconHandler.cs` | `Forms/OrganizeShortcuts.cs:127`, `Wizard/MMC/MMCFile.cs:71` and `Wizard/SpecialCommandsWizard.cs:86,111,154` extract icons from executable files | Retain icon extraction; it supports shortcut and wizard UI |
| `MagicPaket.cs` / `MagicPacket` | `Network/Tools/WakeOnLan.cs:18` | Retain magic-packet format; offloading the UDP send is a separate responsiveness task |
| `Unified.Network.SNTP.NTPClient` | `Network/Tools/NTP/NetworkTime.cs:17–45` | Retain until the separate asynchronous NTP migration |
| `WhoisResolver` | `Network/Tools/WhoIs/WhoIs.cs:21,30` | Retain until the separate bounded asynchronous WHOIS migration |
| `Unified.Serialize` — XML methods | `Data/Credentials/StoredCredentials.cs:99,195`, `Data/DB/FavoriteDetails.cs:121,173`, `Data/History/ConnectionHistory.cs:144,165` and `Settings/ToolStripSettings.cs:34` | Retain current XML method behavior and persisted data formats |
| `Unified.Serialize` — binary methods | Legacy `Network/Services/Server.cs:90` and `Client.cs:92` | Retired in 5.0.0 alongside the network scanner's Direct Connection tab |
| `Unified.StreamHelper` | The XML serializer passed only `MemoryStream` | Retired after replacing its conversion calls with `MemoryStream.ToArray()` and `Encoding.UTF8.GetString()` |
| Encryption helpers | `Source/Terminals/Security/PasswordFunctions.cs` imports `Unified.Encryption` and `Unified.Encryption.Hash` | Preserve existing password formats and DPAPI compatibility until an explicitly tested migration |
| Office 2007 rendering | Rendering files remain compiled by `Terminals.External.csproj` and the theming functionality is retained | Do not fold UI theming changes into dependency cleanup |

References above refer to files at the stated baseline. A source reference establishes a compile-time or code-level consumer, not full runtime acceptance.

## Legacy TCP favorites sharing and BinaryFormatter

`Network/Services/Server.cs` and `Client.cs` are compiled by the Terminals application, and they retain a `BinaryFormatter` wire format through `Unified.Serialize`. `Server.Start` would bind a TCP listener to `IPAddress.Any` on port **1216**. The server receives a username buffer and then sends serialized favorites without a visible authentication or authorization check. The client deserializes a TCP response using `BinaryFormatter.Deserialize`.

This is a concrete **unsafe deserialization path if invoked with attacker-controlled input**, consistent with Microsoft's [BinaryFormatter security guidance](https://learn.microsoft.com/en-us/dotnet/standard/serialization/binaryformatter-security-guide). The Network Scanner's **Direct Connection** tab contained the entry points for starting the TCP listener and connecting a client. The first retirement commit missed those active callers and failed compilation; a follow-up removed the tab and handlers while retaining the port scanner. The sender and receiver also depend on a historical binary wire contract, so simply deleting `SerializeBinary` and `DeSerializeBinary` would break these compiled callers.

The client currently reads a single buffer of at most 4096 bytes before attempting deserialization; it does not implement complete message framing. If this feature is supported, that is an independent correctness issue as well as the unsafe deserialization issue. The retired TCP protocol has no replacement. Users retain normal local favorites import/export capabilities. The WCF named-pipe single-instance command service is separate and unchanged.

## StreamHelper read semantics

`StreamToBytes` allocates using `Stream.Length` and calls `Stream.Read` once without checking the returned count. This would be incorrect for an arbitrary stream that permits short reads. The currently identified serializer call sites use `MemoryStream`, so no user-visible truncation has been established from that helper. After retirement, `SerializeXMLAsBytes` uses `MemoryStream.ToArray()`, and `SerializeXMLAsString` uses `Encoding.UTF8.GetString()` over that array. No general-purpose stream-reading contract is introduced.

## Cleanup constraints

`Terminals.External` is a separately compiled assembly exposing some public utility classes. Absence of an in-repository method call is not sufficient evidence that external plugins or older integrations never reference it. The legacy favorites sharing files and the now-unneeded `StreamHelper.cs` were removed in narrow changes. This remains a historical source audit; removal of other public API requires a separate compatibility assessment.

The immediate runtime work remains in the permanent roadmap: WMI/Services responsiveness, NTP and WHOIS asynchronous operations, and release compatibility checks. This document does not change their status. The single roadmap and status tracker remains PR #6. The two code changes were merged separately to `master`.
