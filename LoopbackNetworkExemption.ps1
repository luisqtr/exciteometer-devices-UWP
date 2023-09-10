param([switch]$Elevated)

function Test-Admin {
    $currentUser = New-Object Security.Principal.WindowsPrincipal $([Security.Principal.WindowsIdentity]::GetCurrent())
    $currentUser.IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)
}

if ((Test-Admin) -eq $false)  {
    if ($elevated) {
        # tried to elevate, did not work, aborting
    } else {
        Start-Process powershell.exe -Verb RunAs -ArgumentList ('-noprofile -noexit -file "{0}" -elevated' -f ($myinvocation.MyCommand.Definition))
    }
    exit
}

'Running with admin privileges'

CheckNetIsolation LoopbackExempt -a -n="10247LuisQuintero.ExciteOMeter_k7zc7t0y9176w"
CheckNetIsolation LoopbackExempt -is -n="10247LuisQuintero.ExciteOMeter_k7zc7t0y9176w"