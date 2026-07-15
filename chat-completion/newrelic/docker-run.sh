#!/bin/bash

docker run --rm \        
    -e NEW_RELIC_LICENSE_KEY \
    -e MSFT_FOUNDRY_ENDPOINT \
    -e MSFT_FOUNDRY_API_KEY \
    -v "$(pwd)/nr-logs:/usr/local/newrelic-dotnet-agent/logs" \
    foundry-newrelic