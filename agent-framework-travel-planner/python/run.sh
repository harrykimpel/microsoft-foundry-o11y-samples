#!/bin/bash

# Run the Flask-based Travel Planner Web Application

echo "🤖 Travel Agent Planner"
echo "=============================="
echo ""

# Check if Python is installed
if ! command -v python3 &> /dev/null; then
    echo "❌ Python 3 is not installed. Please install Python 3.8 or higher."
    exit 1
fi

echo "✓ Python 3 found"

# Create virtual environment if it doesn't exist
if [ ! -d ".venv" ]; then
    echo ""
    echo "Creating virtual environment..."
    python3 -m venv .venv
    echo "✓ Virtual environment created"
fi

# Activate virtual environment
echo ""
echo "Activating virtual environment..."
source .venv/bin/activate || . .venv/Scripts/activate
echo "✓ Virtual environment activated"

# Install requirements
echo ""
echo "Installing dependencies..."
pip3 install -q -r requirements.txt
echo "✓ Dependencies installed"

# Check for .env file
echo ""
if [ ! -f ".env" ]; then
    echo "⚠️  No .env file found. Creating from .env.example..."
    cp .env.example .env
    echo "📝 Created .env file - Please edit it and add your API keys!"
    echo ""
    echo "Required API keys:"
    echo "  - OpenAI: OPENAI_API_KEY"
    echo "  - Azure OpenAI: AZURE_OPENAI_* variables"
    echo "  - Google Gemini: GEMINI_API_KEY"
    echo "  - GitHub Models: GITHUB_TOKEN"
    echo "  - AWS Bedrock: AWS credentials (aws configure)"
    echo ""
    echo "Edit .env now and re-run this script."
    exit 0
else
    echo "✓ .env file found"
fi

# Run the application
echo ""
echo "Starting Travel Agent Planner..."
echo "📱 Open http://localhost:5002 in your browser"
echo ""
echo "Press Ctrl+C to stop the server"
echo ""

export OTEL_TRACES_EXPORTER=otlp
export OTEL_METRICS_EXPORTER=otlp
export OTEL_LOGS_EXPORTER=otlp
# US region
export OTEL_EXPORTER_OTLP_ENDPOINT='https://otlp.nr-data.net'
# EU region
#export OTEL_EXPORTER_OTLP_ENDPOINT='https://otlp.eu01.nr-data.net'
export OTEL_EXPORTER_OTLP_HEADERS="api-key=$NEW_RELIC_LICENSE_KEY"
export OTEL_SERVICE_NAME="agent-travel-planner"

export OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true
export OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_METADATA=true
export OTEL_INSTRUMENTATION_GENAI_CAPTURE_TOOL_OUTPUT=true
export OTEL_INSTRUMENTATION_GENAI_CAPTURE_TOOL_INPUT=true
export OTEL_PYTHON_LOGGING_AUTO_INSTRUMENTATION_ENABLED=true

export ENABLE_OTEL=true
export ENABLE_SENSITIVE_DATA=true
export OTLP_ENDPOINT='https://otlp.nr-data.net'
export OTLP_HEADERS="api-key=$NEW_RELIC_LICENSE_KEY"

export GITHUB_TOKEN="$GITHUB_TOKEN"
export GITHUB_ENDPOINT="https://models.github.ai/inference"
#export GITHUB_MODEL_ID="gpt-5-mini"
#export OPENAI_CHAT_MODEL_ID="gpt-5-mini"
export MSFT_FOUNDRY_ENDPOINT="$MSFT_FOUNDRY_ENDPOINT" # e.g., https://your-resource-name.openai.azure.com/openai/v1/
export MSFT_FOUNDRY_API_KEY="$MSFT_FOUNDRY_API_KEY"

# Run the Flask application
python web_app.py
