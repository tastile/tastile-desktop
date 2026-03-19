$thumbprint = '05F80262D0FFB902EC85C097A6DD2FFFA07D2761'
$cert = Get-ChildItem -Path "Cert:\CurrentUser\My\$thumbprint"
$pwd = ConvertTo-SecureString -String 'tastile-dev' -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath 'C:\Users\rebui\Desktop\tastile\tastile-desktop\src\TastileDesktop\TastileDev.pfx' -Password $pwd
Write-Output "Exported to TastileDev.pfx"
