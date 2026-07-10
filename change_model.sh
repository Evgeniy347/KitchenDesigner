#!/bin/bash

set -e

if [ $# -eq 0 ]; then
    echo "Using: ./change_model.sh \"<new_model>\""
    echo ""
    echo "Examples:"
    echo "  ./change_model.sh \"opencode/big-pickle\""
    echo "  ./change_model.sh \"arcee-ai/trinity-large-preview:free\""
    echo "  ./change_model.sh \"gpt-4-turbo\""
    exit 1
fi

NEW_MODEL="$1"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
AGENTS_DIR="${SCRIPT_DIR}/.opencode/agents"
CONFIG_FILE="${SCRIPT_DIR}/.opencode/opencode.json"

echo "🔄 Change model to: $NEW_MODEL"
echo ""

update_json_model() {
    local file="$1"
    local temp_file="${file}.tmp"
    
    sed -E 's|"model": "[^"]*"|"model": "'"$NEW_MODEL"'"|g' "$file" > "$temp_file"
    
    if [ -s "$temp_file" ]; then
        mv "$temp_file" "$file"
        return 0
    else
        rm "$temp_file"
        return 1
    fi
}

update_markdown_model() {
    local file="$1"
    local temp_file="${file}.tmp"
    
    sed -E 's|^model: .*$|model: '"$NEW_MODEL"'|' "$file" > "$temp_file"
    
    if [ -s "$temp_file" ]; then
        mv "$temp_file" "$file"
        return 0
    else
        rm "$temp_file"
        return 1
    fi
}

updated_agents=0
updated_json=0

if [ -d "$AGENTS_DIR" ]; then
    echo "📝 Updating .opencode/agents/*.md..."
    for agent_file in "$AGENTS_DIR"/*.md; do
        if [ -f "$agent_file" ]; then
            if grep -q "^model:" "$agent_file"; then
                update_markdown_model "$agent_file"
                agent_name=$(basename "$agent_file")
                echo "  ✓ $agent_name"
                ((updated_agents++))
            fi
        fi
    done
fi

echo ""

if [ -f "$CONFIG_FILE" ]; then
    echo "⚙️  Updating .opencode/opencode.json..."
    
    model_count=$(grep -c '"model":' "$CONFIG_FILE" || true)
    
    if [ "$model_count" -gt 0 ]; then
        update_json_model "$CONFIG_FILE"
        echo "  ✓ Updated $model_count records in opencode.json"
        updated_json=$model_count
    fi
else
    echo "⚠️  File $CONFIG_FILE not found"
fi

echo ""
echo "✅ Ready!"
echo "   • Agents added: $updated_agents"
echo "   • Records in opencode.json updated: $updated_json"
echo "   • New model: $NEW_MODEL"
