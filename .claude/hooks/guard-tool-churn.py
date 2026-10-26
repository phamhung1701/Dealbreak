"""PreToolUse hook: block a git commit that would include tool churn.

Packages/com.farlocus.locus, Locus/ (except Locus/knowledge notes) and .plastic/
are rewritten by tools while Unity runs. CLAUDE.md says never to commit them;
this makes the rule hard. Exit 2 blocks the call and shows stderr to Claude.
"""
import json
import re
import subprocess
import sys

CHURN = re.compile(r"^(Packages/com\.farlocus\.locus/|\.plastic/|Locus/(?!knowledge/))")


def git_names(*args):
    out = subprocess.run(["git", *args], capture_output=True, text=True)
    return [line for line in out.stdout.splitlines() if line]


def main():
    try:
        command = json.load(sys.stdin).get("tool_input", {}).get("command", "")
    except (ValueError, AttributeError):
        return 0
    if not re.search(r"\bgit\b[^\n;&|]*\bcommit\b", command, re.IGNORECASE):
        return 0

    paths = git_names("diff", "--cached", "--name-only")
    # `commit -a`/`-am`/`--all` also takes every modified tracked file.
    if re.search(r"\bcommit\b[^\n;&|]*\s(-[a-zA-Z]*a[a-zA-Z]*|--all)\b", command):
        paths += git_names("diff", "--name-only")
    # The hook runs before the whole command, so `git add -A && git commit`
    # has not staged anything yet: count what a broad add would take.
    if re.search(r"\bgit\s+add\b[^\n;&|]*\s(\.|-A|--all|-u|--update)(\s|$)", command):
        paths += git_names("diff", "--name-only")
        paths += git_names("ls-files", "--others", "--exclude-standard")
    churn = sorted({p for p in paths if CHURN.match(p)})
    if not churn:
        return 0

    shown = "\n  ".join(churn[:10]) + ("\n  ..." if len(churn) > 10 else "")
    print(
        "BLOCKED: this commit would include Locus/.plastic tool churn, which "
        "CLAUDE.md says never to commit:\n  " + shown + "\n"
        "Unstage those paths (git restore --staged <path>) and stage only "
        "your own files. Locus/knowledge notes are allowed.",
        file=sys.stderr,
    )
    return 2


if __name__ == "__main__":
    sys.exit(main())
