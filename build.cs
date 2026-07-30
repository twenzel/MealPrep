#:sdk Cake.Sdk@6.2.0

using System.Text.RegularExpressions;

InstallTools(
    "dotnet:?package=GitVersion.Tool&version=6.8.2"
);

var target = Argument("target", "Default");
var imageName = Argument("image-name", "mealprep-app");
var outputDirectoryArgument = Argument("output-directory", "artifacts/docker");
var platform = Argument("platform", string.Empty);
var githubOwner = Argument(
    "github-owner",
    EnvironmentVariable("GITHUB_REPOSITORY_OWNER") ?? string.Empty);
var githubImageName = Argument("github-image-name", imageName);
var pushLatest = Argument("push-latest", false);

//////////////////////////////////////////////////////////////////////
// BUILD VARIABLES
//////////////////////////////////////////////////////////////////////

var repositoryRoot = MakeAbsolute(Directory("."));
var dockerfile = repositoryRoot.CombineWithFilePath("Dockerfile");
var outputDirectory = MakeAbsolute(Directory(outputDirectoryArgument));

var imageTag = string.Empty;
var imageReference = string.Empty;
var archivePath = outputDirectory.CombineWithFilePath("pending.tar");

void RunCommand(
    FilePath executable,
    ProcessArgumentBuilder arguments,
    DirectoryPath workingDirectory)
{
    Information("> {0} {1}", executable, arguments.RenderSafe());

    var exitCode = StartProcess(
        executable,
        new ProcessSettings
        {
            Arguments = arguments,
            WorkingDirectory = workingDirectory
        });
    if (exitCode != 0)
    {
        throw new Exception($"{executable} wurde mit Exit-Code {exitCode} beendet.");
    }
}

string ToDockerTag(string version)
{
    var normalized = version.Replace("+", "-build.", StringComparison.Ordinal);
    normalized = Regex.Replace(normalized, @"[^A-Za-z0-9_.-]+", "-").Trim('.', '-');

    if (string.IsNullOrWhiteSpace(normalized))
    {
        throw new Exception($"GitVersion lieferte keinen gültigen Docker-Tag: '{version}'.");
    }

    if (normalized.Length > 128)
    {
        throw new Exception(
            $"Der aus GitVersion ermittelte Docker-Tag ist mit {normalized.Length} Zeichen zu lang.");
    }

    return normalized;
}

string ToArchiveName(string value)
{
    var normalized = Regex.Replace(value, @"[^A-Za-z0-9_.-]+", "-").Trim('.', '-');
    return string.IsNullOrWhiteSpace(normalized) ? "image" : normalized;
}

string ToGitHubImagePath(string owner, string name)
{
    var normalizedOwner = owner.Trim().Trim('/').ToLowerInvariant();
    var normalizedName = name.Trim().Trim('/').ToLowerInvariant();

    if (string.IsNullOrWhiteSpace(normalizedOwner))
    {
        throw new Exception(
            "Für Docker-Push muss --github-owner angegeben oder " +
            "GITHUB_REPOSITORY_OWNER gesetzt sein.");
    }

    if (string.IsNullOrWhiteSpace(normalizedName))
    {
        throw new Exception("--github-image-name darf nicht leer sein.");
    }

    if (normalizedOwner.Contains('/') ||
        normalizedOwner.Contains('\\') ||
        normalizedName.Contains('\\') ||
        normalizedName.Any(char.IsWhiteSpace))
    {
        throw new Exception(
            $"Ungültiger GitHub-Containerpfad: '{owner}/{name}'.");
    }

    return $"ghcr.io/{normalizedOwner}/{normalizedName}";
}

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Setup(context =>
{
    Information($"Repository:       {repositoryRoot.FullPath}");
    Information($"Dockerfile:       {dockerfile.FullPath}");
    Information($"Image name:       {imageName}");
    Information($"Output directory: {outputDirectory.FullPath}");
    Information($"Platform:         {(string.IsNullOrWhiteSpace(platform) ? "Docker-Standard" : platform)}");
});

GitVersion? versionInfo = null;
Task("Version")
    .Description("Retrieves the current version from the git repository")
    .Does(() =>
{
    versionInfo = GitVersion(new GitVersionSettings
    {
        UpdateAssemblyInfo = false
    });

    imageTag = ToDockerTag(versionInfo.FullSemVer);
    imageReference = $"{imageName}:{imageTag}";
    archivePath = outputDirectory.CombineWithFilePath(
        $"{ToArchiveName(imageName)}-{ToArchiveName(imageTag)}.tar");

    Information("MajorMinorPatch: {0}", versionInfo.MajorMinorPatch);
    Information("SemVer:         {0}", versionInfo.SemVer);
    Information("FullSemVer:     {0}", versionInfo.FullSemVer);
    Information("AssemblySemVer: {0}", versionInfo.AssemblySemVer);
    Information("AssemblyFile:   {0}", versionInfo.AssemblySemFileVer);
    Information("Branch:         {0}", versionInfo.BranchName);
    Information("SHA:            {0}", versionInfo.Sha);
    Information("Docker image:   {0}", imageReference);
    Information("Archive:        {0}", archivePath);
});

Task("Docker-Build")
    .Description("Builds the versioned Docker image")
    .IsDependentOn("Version")
    .Does(() =>
{
    if (versionInfo is null)
    {
        throw new InvalidOperationException("GitVersion information is not available.");
    }

    var arguments = new ProcessArgumentBuilder()
        .Append("build")
        .Append("--file")
        .AppendQuoted(dockerfile.FullPath)
        .Append("--tag")
        .AppendQuoted(imageReference)
        .Append("--build-arg")
        .AppendQuoted($"VERSION={versionInfo.SemVer}")
        .Append("--build-arg")
        .AppendQuoted($"ASSEMBLY_VERSION={versionInfo.AssemblySemVer}")
        .Append("--build-arg")
        .AppendQuoted($"FILE_VERSION={versionInfo.AssemblySemFileVer}")
        .Append("--build-arg")
        .AppendQuoted($"INFORMATIONAL_VERSION={versionInfo.InformationalVersion}")
        .Append("--label")
        .AppendQuoted($"org.opencontainers.image.version={versionInfo.FullSemVer}")
        .Append("--label")
        .AppendQuoted($"org.opencontainers.image.revision={versionInfo.Sha}");

    if (!string.IsNullOrWhiteSpace(platform))
    {
        arguments
            .Append("--platform")
            .AppendQuoted(platform);
    }

    arguments.AppendQuoted(repositoryRoot.FullPath);
    RunCommand("docker", arguments, repositoryRoot);
});

Task("Docker-Export")
    .Description("Exports the versioned Docker image as a TAR archive")
    .IsDependentOn("Docker-Build")
    .Does(() =>
{
    CreateDirectory(outputDirectory);
    if (FileExists(archivePath))
    {
        DeleteFile(archivePath);
    }

    var arguments = new ProcessArgumentBuilder()
        .Append("save")
        .Append("--output")
        .AppendQuoted(archivePath.FullPath)
        .AppendQuoted(imageReference);
    RunCommand("docker", arguments, repositoryRoot);

    var archive = new System.IO.FileInfo(archivePath.FullPath);
    if (!archive.Exists || archive.Length == 0)
    {
        throw new Exception($"Das Docker-Archiv wurde nicht erstellt: {archivePath}");
    }

    Information(
        "Docker-Archiv erstellt: {0} ({1:F1} MiB)",
        archivePath,
        archive.Length / 1024d / 1024d);
});

Task("Docker-Push")
    .Description("Pushes the versioned Docker image to GitHub Container Registry")
    .IsDependentOn("Docker-Build")
    .Does(() =>
{
    var githubImagePath = ToGitHubImagePath(githubOwner, githubImageName);
    var versionedReference = $"{githubImagePath}:{imageTag}";

    RunCommand(
        "docker",
        new ProcessArgumentBuilder()
            .Append("tag")
            .AppendQuoted(imageReference)
            .AppendQuoted(versionedReference),
        repositoryRoot);
    RunCommand(
        "docker",
        new ProcessArgumentBuilder()
            .Append("push")
            .AppendQuoted(versionedReference),
        repositoryRoot);

    Information("GitHub-Image veröffentlicht: {0}", versionedReference);

    if (pushLatest)
    {
        var latestReference = $"{githubImagePath}:latest";
        RunCommand(
            "docker",
            new ProcessArgumentBuilder()
                .Append("tag")
                .AppendQuoted(imageReference)
                .AppendQuoted(latestReference),
            repositoryRoot);
        RunCommand(
            "docker",
            new ProcessArgumentBuilder()
                .Append("push")
                .AppendQuoted(latestReference),
            repositoryRoot);

        Information("GitHub-Image veröffentlicht: {0}", latestReference);
    }
});

Task("Default")
    .IsDependentOn("Docker-Export");

RunTarget(target);
