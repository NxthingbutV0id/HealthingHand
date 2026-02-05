# Healthing Hand

A simple health-tracking web app for our SE 300 project:
- Sleep tracking
- Calorie/diet logging (incl. nutrition label scan -> auto entry)
- Workout routines
- User accounts (create/login/settings/deletion)
- Secure per-user data storage (no sharing between users)

> This README is also a guide to Git/GitHub. Read it to know how to contribute

---

## What you need installed

### Required
- **Git** (command line)  
  - macOS: `brew install git` (or install via Xcode Command Line Tools)
  - Windows: install “Git for Windows”
- **.NET SDK** (the version our repo targets)
  - If the repo includes a `global.json`, install **that** SDK version.
  - Otherwise, install the **latest stable .NET SDK** available on your machine.

### Optional (pick one editor)
- **Visual Studio** (Windows)
- **VS Code** + C# Dev Kit
- **JetBrains Rider** (What Christian uses)

---

## First-time Git setup (do this once)

Open a terminal and run:

```bash
git config --global user.name "Your Name"
git config --global user.email "your@email.com"
git config --global pull.rebase true
````

Check it worked:

```bash
git config --global --list
```

---

## Clone the repo (get the code on your computer)

Pick a folder you want to work in, then:

```bash
git clone <PASTE_REPO_URL_HERE>
cd <REPO_FOLDER_NAME>
```

> On GitHub, click the green **Code** button → copy the HTTPS URL.

---

## Run the project locally

From the repo root:

```bash
dotnet restore
dotnet build
dotnet run
```

If you want auto-reload while coding:

```bash
dotnet watch run
```

### If you see HTTPS certificate warnings (common on first run)

Run:

```bash
dotnet dev-certs https --trust
```

Then try `dotnet run` again.

---

## Our GitHub workflow (how to contribute)

**Rule #1: Don’t commit directly to `main`.**
Work on a branch, push it, then open a Pull Request (PR).

### 1) Sync your local `main`

```bash
git checkout main
git pull
```

### 2) Create a new branch for your work

Name it something like: `feature/login`, `feature/diet-scan`, `fix/navbar`, etc.

```bash
git checkout -b feature/short-description
```

### 3) Make changes + commit them

```bash
git status
git add .
git commit -m "Add <what you changed>"
```

### 4) Push your branch to GitHub

```bash
git push -u origin feature/short-description
```

### 5) Open a Pull Request (PR)

On GitHub you should see a banner offering to “Compare & pull request”.

* Add a clear title + short description
* Link the issue/task if we’re using GitHub Issues/Projects
* Request review from at least 1 teammate (if we’re doing reviews)

### 6) Keep your branch up to date (before merging)

```bash
git checkout main
git pull
git checkout feature/short-description
git merge main
```

If there are merge conflicts, ask in the group chat—we’ll resolve them together.

---

## Git cheat sheet (most used commands)

```bash
git status                 # see what changed
git branch                 # list branches
git checkout <branch>      # switch branches
git checkout -b <branch>   # create + switch to new branch
git pull                   # get latest changes
git add <file> / git add . # stage changes
git commit -m "message"    # commit staged changes
git push                   # push to GitHub
```

---

## If you REALLY don’t want to use the terminal

You can use:

* **GitHub Desktop** (clone, commit, push, PRs with buttons)
* **Visual Studio** Git integration
* **VS Code Source Control** panel

(Still follow the same branch → PR workflow.)

---

## Troubleshooting

### “dotnet command not found”

Install the .NET SDK, then reopen your terminal/editor.

### Build errors after pulling

Try a clean build:

```bash
dotnet clean
dotnet build
```

### “I accidentally changed files / I’m stuck”

Don’t panic—tell the team what happened. Git usually can undo almost anything.

---

## Team conventions (quick)

* Make small commits with clear messages
* One feature/fix per branch
* Open a PR early if you want help (Draft PR is fine)
* Don’t commit secrets (API keys/passwords). If you’re unsure, ask first.

---
