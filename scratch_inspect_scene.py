import os

log_path = r"C:\Users\Чазик\AppData\Local\Unity\Editor\Editor.log"

if os.path.exists(log_path):
    with open(log_path, "r", encoding="utf-8", errors="ignore") as f:
        text = f.read()
    
    # Split logs by log separator or just process block by block
    # Unity logs have timestamps or filenames
    # Let's find occurrences of scripts like AICarDriver, EzerealCarController, RaceManager
    keywords = ["AICarDriver", "EzerealCarController", "RaceManager", "CatmullRomSpline", "Waypoint"]
    
    # Let's find lines with these keywords and print their surrounding context (5 lines before and 10 lines after)
    lines = text.split("\n")
    printed_indices = set()
    
    print("--- Searching for log entries matching project keywords ---")
    for i, line in enumerate(lines):
        if any(k in line for k in keywords):
            # Check if this line is part of an exception or error
            # We print a window around it
            start = max(0, i - 3)
            end = min(len(lines), i + 10)
            
            # Skip if we already printed this window
            if any(idx in printed_indices for idx in range(start, end)):
                continue
                
            print(f"\n--- Context near line {i+1} ---")
            for j in range(start, end):
                print(f"{j+1}: {lines[j]}")
                printed_indices.add(j)
else:
    print("Log file not found!")
