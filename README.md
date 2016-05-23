# EventHubData
Uses the Azure Management REST API to pull usage metrics for an event hub. Could be altered to pull metrics for other types of resources.

To use, replace the following values in the config file:

1. "SubscriptionId" - the ID of the subscription to use
1. "CertificateThumbprint" - the thumbprint of the certificate to use (must be previously created and uploaded to Azure)
1. "ServiceBusNamespace" - the namespace of the Service Bus the Event Hub resides in
1. "EventHubName" - the name of the Event Hub
1. "MetricsToWatch" - pipe delimited list of the metrics to watch. The list of available metrics can be found by running the application with the getmetrics command line argument
1. "RollupPeriod" - the rollup period to use. One of the following: PT5M, PT1H, P1D, P7D. See app.config for definitions
1. "HowFarToLookBackInMinutes" - how far back in time to look when requesting metric data

The command line program can be run in two modes:

Usage: 
```
EventHubData showavail|getmetrics
showavail -  show all metrics available for the event hub in the config
getmetrics - stream metrics every 5 min for the event hub in the config
```

When run in *getmetrics* mode, it will create a file in the local directory with the name *EventHubMetrics_[CURRENT_DATE].csv*