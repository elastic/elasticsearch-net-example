open System

[<CLIMutable>]
type FeedPackage =
    {
        Id: string
        Version: string
        Authors: string
        Copyright: string
        Created: DateTime
        Dependencies: string
        Description: string
        DownloadCount: int
        GalleryDetailsUrl: string
        IconUrl: string
        IsLatestVersion: bool
        IsAbsoluteLatestVersion: bool
        IsPreRelease: bool

        Language: string
        LastUpdated: DateTime
        Published: DateTime
        PackageHash: string
        PackageHashAlgorithm: string
        PackageSize: int64
        PackageUrl: string
        ReportAbuseUrl: string
        ReleaseNotes: string
        RequireLicenseAcceptance: bool
        Summary: string
        Tags: string
        Title: string
        VersionDownloadCount: int
        MinClientVersion: string
        LastEdited: DateTime

        LicenseUrl: string
        LicenseNames: string
        LicenseReportUrl: string
    }

[<CLIMutable>]
type NugetDump =
    {
        NugetPackages: System.Collections.Generic.List<FeedPackage>
    }

[<CLIMutable>]
type PackageAuthor =
    {
        Name: string
    }

[<CLIMutable>]
type PackageDependency =
    {
        Name: string
        Version: string
        Framework: string
    }

[<CLIMutable>]
type PackageVersion =
    {
        Version: string
        Created: DateTime
        Dependencies: PackageDependency list
        Description: string
        GalleryDetailsUrl: string
        IconUrl: string
        IsLatestVersion: bool
        IsAbsoluteLatestVersion: bool
        IsPreRelease: bool
        Language: string
        LastUpdated: DateTime
        Published: DateTime
        PackageHash: string
        PackageHashAlgorithm: string
        PackageSize: int64
        PackageUrl: string
        ReportAbuseUrl: string
        ReleaseNotes: string
        RequireLicenseAcceptance: bool
        Summary: string
        Tags: string
        Title: string
        DownloadCount: int
        MinClientVersion: string
        LastEdited: DateTime
        LicenseUrl: string
        LicenseNames: string
        LicenseReportUrl: string
    }

[<CLIMutable>]
type Package =
    {
        Id: string
        IconUrl: string
        Summary: string
        Authors: PackageAuthor list
        Versions: PackageVersion list
        Copyright: string
        DownloadCount: int
    }
