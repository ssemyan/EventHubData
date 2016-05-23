# To use the management APIS you will first need to create a self-signed certificate and then upload it to Azure.

# First Command (this creates the certificate in the local store). Replace [YOUR_DNS_NAME] with the name you wish to use for this certificate. 
New-SelfSignedCertificate -CertStoreLocation Cert:\CurrentUser\My -DnsName [YOUR_DNS_NAME]

# After the first command runs, it will report the thumbprint of the new certificate. 
# Use this thumbprint in the next command to export the certificate so it can be uploaded to Azure
Export-Certificate -cert Cert:\CurrentUser\My\[THUMBPRINT] -FilePath c:\mycert.cer 