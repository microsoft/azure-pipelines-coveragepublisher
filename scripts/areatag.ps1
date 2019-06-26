$buildReason = ${env:BUILD_REASON}

if($buildReason -eq "PullRequest")
{
    Write-Host "##vso[build.addbuildtag]PR"
}
else
{
    Write-Host "##vso[build.addbuildtag]CI"
}