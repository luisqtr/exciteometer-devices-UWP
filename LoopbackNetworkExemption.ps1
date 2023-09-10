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

'Excite-O-Meter | Devices URP - EoM'
'This application allows sending LSL streams to the same PC where the EoM is running'
'... Please keep this window open for as long as you want to use the Excite-O-Meter Devices'

CheckNetIsolation LoopbackExempt -a -n="10247LuisQuintero.ExciteOMeter_k7zc7t0y9176w"
CheckNetIsolation LoopbackExempt -is -n="10247LuisQuintero.ExciteOMeter_k7zc7t0y9176w"