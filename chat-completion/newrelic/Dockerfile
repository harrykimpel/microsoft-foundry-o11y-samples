FROM mcr.microsoft.com/dotnet/sdk:9.0

# Override to pin a specific agent version, e.g. --build-arg NEW_RELIC_AGENT_VERSION=10.41.1
# When unset, the build resolves the latest release from the upstream GitHub repo.
ARG NEW_RELIC_AGENT_VERSION=""

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

RUN set -eux; \
    ARCH="$(dpkg --print-architecture)"; \
    VERSION="${NEW_RELIC_AGENT_VERSION:-$(curl -fsSL https://api.github.com/repos/newrelic/newrelic-dotnet-agent/releases/latest \
        | grep '"tag_name"' | head -n1 | cut -d'"' -f4 | sed 's/^v//')}"; \
    echo "Installing New Relic .NET agent ${VERSION} for ${ARCH}"; \
    curl -fsSL -o /tmp/newrelic.tar.gz \
        "https://download.newrelic.com/dot_net_agent/previous_releases/${VERSION}/newrelic-dotnet-agent_${VERSION}_${ARCH}.tar.gz"; \
    mkdir -p /tmp/nr-extract /usr/local/newrelic-dotnet-agent; \
    tar -xzf /tmp/newrelic.tar.gz -C /tmp/nr-extract; \
    entries="$(find /tmp/nr-extract -maxdepth 1 -mindepth 1)"; \
    if [ "$(echo "$entries" | wc -l)" = "1" ] && [ -d "$entries" ]; then \
        mv "$entries"/* /usr/local/newrelic-dotnet-agent/; \
    else \
        mv /tmp/nr-extract/* /usr/local/newrelic-dotnet-agent/; \
    fi; \
    rm -rf /tmp/nr-extract /tmp/newrelic.tar.gz; \
    chmod +x /usr/local/newrelic-dotnet-agent/run.sh

WORKDIR /app

COPY chat-completion.csproj ./
RUN dotnet restore

COPY Program.cs run.sh ./
RUN chmod +x run.sh

ENV DOTNET_MULTILEVEL_LOOKUP=0 \
    TARGET=net9.0

ENTRYPOINT ["./run.sh"]
