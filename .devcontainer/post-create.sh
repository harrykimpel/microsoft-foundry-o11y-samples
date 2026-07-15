#!/usr/bin/env bash
set -euo pipefail

# Install the New Relic .NET agent so the chat-completion/newrelic sample can
# attach the CLR profiler. The agent installs to /usr/local/newrelic-dotnet-agent,
# which is what chat-completion/newrelic/run.sh expects via CORECLR_NEWRELIC_HOME.
# Both linux-amd64 and linux-arm64 packages are published, so apt picks the
# right one for the container's architecture (matters on Apple Silicon hosts).

if [ ! -d /usr/local/newrelic-dotnet-agent ]; then
  sudo install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.newrelic.com/548C16BF.gpg \
    | sudo gpg --dearmor -o /etc/apt/keyrings/newrelic.gpg
  echo "deb [signed-by=/etc/apt/keyrings/newrelic.gpg] http://apt.newrelic.com/debian/ newrelic non-free" \
    | sudo tee /etc/apt/sources.list.d/newrelic.list >/dev/null
  sudo apt-get update
  sudo apt-get install -y newrelic-dotnet-agent
fi
