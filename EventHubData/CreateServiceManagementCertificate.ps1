# https://www.simple-talk.com/cloud/security-and-compliance/windows-azure-management-certificates/
# https://azure.microsoft.com/en-us/documentation/articles/azure-api-management-certs/
# http://windowsitpro.com/blog/creating-self-signed-certificates-powershell


New-SelfSignedCertificate -CertStoreLocation Cert:\CurrentUser\My -DnsName scottse-surf.redmond.corp.microsoft.com
Export-Certificate -cert Cert:\CurrentUser\My\4CB46D497D538567F3EC18030FC3BACE46E15AE0 -FilePath c:\mycert.cer 