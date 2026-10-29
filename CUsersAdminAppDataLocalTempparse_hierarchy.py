import yaml
import sys
from pathlib import Path

def extract_hierarchy(prefab_path):
    try:
        with open(prefab_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Parse YAML
        docs = list(yaml.safe_load_all(content))
        
        # Build name->id and id->children maps
        name_map = {}  # id -> name
        children_map = {}  # id -> [child_ids]
        parent_map = {}  # id -> parent_id
        root_id = None
        
        for doc in docs:
            if doc is None:
                continue
            if 'GameObject' in str(type(doc)) or (isinstance(doc, dict) and 'm_Name' in doc):
                # This is tricky - we need to track which doc is which
                pass
        
        # Simple regex approach instead
        import re
        
        # Find all GameObjects and their hierarchy info
        go_pattern = r'--- !u!1 &(\d+)\nGameObject:.*?m_Name: (.*?)\n.*?m_Children:\n((?:  - \{fileID: \d+\}\n)*)?.*?m_Father: \{fileID: (\d+)\}'
        
        for match in re.finditer(go_pattern, content, re.DOTALL):
            obj_id = match.group(1)
            name = match.group(2).strip()
            children_str = match.group(3) or ""
            father_id = match.group(4)
            
            name_map[obj_id] = name
            parent_map[obj_id] = father_id
            
            # Parse children
            child_ids = []
            for child_match in re.finditer(r'\{fileID: (\d+)\}', children_str):
                child_ids.append(child_match.group(1))
            
            if child_ids:
                children_map[obj_id] = child_ids
        
        # Find root (where father is 0)
        root_id = None
        for obj_id, father_id in parent_map.items():
            if father_id == '0':
                root_id = obj_id
                break
        
        # Build tree
        def build_tree(obj_id, depth=0):
            if obj_id not in name_map:
                return None
            name = name_map[obj_id]
            if not name or name.startswith('---'):
                return None
            
            result = f"{'  ' * depth}{name}"
            children = children_map.get(obj_id, [])
            for child_id in children:
                child_tree = build_tree(child_id, depth + 1)
                if child_tree:
                    result += f"\n{child_tree}"
            return result
        
        if root_id:
            return build_tree(root_id)
        return "No root found"
    except Exception as e:
        return f"Error: {e}"

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: parse_hierarchy.py <prefab_path>")
        sys.exit(1)
    
    result = extract_hierarchy(sys.argv[1])
    print(result)
