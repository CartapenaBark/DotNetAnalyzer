---
name: dotnet-analyzer-code-intelligence
description: >
  Navigate and understand C# code through symbol queries, type hierarchies, call graphs,
  and semantic analysis using Roslyn-powered MCP tools.
  Use when the user asks to find references, go to definition, understand type inheritance,
  explore call hierarchies, get symbol info, analyze code metrics, or traverse syntax trees.
  USE FOR: finding all references to a symbol, locating definitions, getting type hierarchy,
  understanding class inheritance chains, finding method overrides and interface implementations,
  getting caller/callee info, generating call graphs, navigating to definitions,
  resolving symbols, getting semantic model details, analyzing syntax trees,
  calculating code metrics (cyclomatic complexity, maintainability index).
  DO NOT USE FOR: understanding overall project structure or listing projects
  → use dotnet-analyzer-project-analysis instead.
  DO NOT USE FOR: checking code quality, detecting code smells, or architecture violations
  → use dotnet-analyzer-code-quality instead.
  Covers .NET, C#, Roslyn, symbol navigation, type hierarchy, call graph, references,
  definitions, semantic model, syntax tree, code metrics, Go to Definition, Find All References.
---

# .NET Code Intelligence

Navigate and understand C# code through symbol queries, type hierarchies, call graphs, and semantic analysis.

## Prerequisite Check

**Before any analysis, verify dotnet-analyzer is installed:**

1. Run `dotnet-analyzer --version` in a shell
2. If it fails, ask the user: "DotNetAnalyzer global tool is not installed. Shall I run `dotnet tool install --global DotNetAnalyzer` for you?"
   - If confirmed, run the install command and continue
   - If declined, provide the manual install command and stop

## Available MCP Tools

Use these MCP tools (prefixed with `mcp__plugin_netan_dotnet-analyzer__`):

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| `find_references` | Find all references to a symbol | `projectPath`, `filePath`, `line`, `column` |
| `find_declarations` | Find symbol declaration locations | `projectPath`, `filePath`, `line`, `column` |
| `get_symbol_info` | Get detailed symbol metadata | `projectPath`, `filePath`, `line`, `column` |
| `resolve_symbol` | Resolve a symbol by name | `projectPath`, `filePath`, `line`, `column` |
| `go_to_definition` | Jump to symbol definition | `filePath`, `line`, `column` |
| `get_type_hierarchy` | Full type inheritance chain | `projectPath`, `typeName` |
| `get_member_hierarchy` | Member override/implementation chain | `projectPath`, `memberName`, `containingType` |
| `get_semantic_model` | Semantic info at a position | `filePath`, `line`, `column` |
| `get_syntax_tree` | Syntax tree structure as JSON | `filePath`, optional `range`, `maxDepth` |
| `get_code_metrics` | Complexity and quality metrics | `projectPath`, `filePath` |
| `get_caller_info` | Find all callers of a method | `projectPath`, `filePath`, `line`, `column` |
| `get_callee_info` | Find all methods called by a method | `projectPath`, `filePath`, `line`, `column` |
| `get_call_graph` | Generate call graph (DOT/JSON/Mermaid) | `projectPath`, `filePath`, optional `format` |
| `get_definition_and_references` | Combined definition + references | `projectPath`, `filePath`, `line`, `column` |

## Workflow

### Finding References

1. **Locate the symbol**: Get file path, line, and column (0-based)
2. **Call `find_references`**: Returns all reference locations with context snippets
3. **Distinguish definition vs reference**: Each result includes `isDefinition` flag
4. **Present**: Group by file, show context snippets

### Understanding Type Hierarchy

1. **Get type name**: From symbol info or user input
2. **Call `get_type_hierarchy`**: Returns base types, derived types, interfaces, and members
3. **Deep dive**: Use `get_member_hierarchy` for specific member override chains
4. **Present**: Show inheritance tree with file locations

### Analyzing Call Graphs

1. **Locate the method**: Get file path, line, and column
2. **Call `get_call_graph`**: Returns nodes, edges, and complexity metrics
3. **Optional formats**: DOT (Graphviz), JSON, Mermaid, SVG
4. **Present**: Visualize the call graph or summarize key relationships

### Code Metrics

1. **Call `get_code_metrics`**: Returns cyclomatic complexity, lines of code, maintainability index
2. **Review statistics**: Min, max, average, standard deviation, outliers
3. **Flag high-complexity methods**: Cyclomatic complexity > 15

## Output Format

Present findings with:
- Symbol name, kind, and containing type
- File locations with line numbers (clickable links)
- Hierarchical/tree views for type and member relationships
- Summary statistics for metrics
- Context code snippets for references
