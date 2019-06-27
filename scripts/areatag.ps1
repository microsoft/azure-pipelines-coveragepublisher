$buildReason = ${env:BUILD_REASON}

if($buildReason -neq "PullRequest")
{
    Write-Host "##vso[build.addbuildtag]CI"
}
