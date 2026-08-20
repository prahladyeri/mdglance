![license](https://img.shields.io/github/license/prahladyeri/mdglance.svg)
[![patreon](https://img.shields.io/badge/Patreon-brown.svg?logo=patreon)](https://www.patreon.com/prahladyeri)
[![paypal](https://img.shields.io/badge/PayPal-blue.svg?logo=paypal)](https://paypal.me/prahladyeri)
[![follow](https://img.shields.io/twitter/follow/prahladyeri.svg?style=social)](https://x.com/prahladyeri)

Fast, minimal, and dependency-free Markdown viewing on Windows.

## Genesis

My first acquaintance with Markdown syntax was when I started blogging and "redditing" many years ago, it used to be a way of authoring posts and blog articles with software like jekyll back then. But today, it's turned quite ubiquitous and found everywhere and most importantly, it's the language preferred by LLMs like chatgpt and gemini.

Consequently, you need a way to easily browse these important chat logs stored on your computer. My preference for such a tool is one that is both minimal and utilitarian at the same time (which is quite a rarity these days!), and that's how MDGlance was born on 31st May, 2026.

## Install

Just get the latest release build [from here](https://github.com/prahladyeri/mdglance/releases/latest) and extract it inside a directory of your choice such as `C:\Programs\`, create a shortcut and place on your desktop for regular access.

## Screenshots

![MDGlance in Action](assets/screenshot.png)

## Browse an LLM backup

Rather than exporting each chat to its own markdown file, you can point MDGlance straight at the JSON backup your LLM
hands out.

1. In ChatGPT, go to *Settings → Data controls → Export data*. The mail you get back contains a `conversations.json`.
2. In MDGlance, pick *File → Open LLM Backup...* and select that file.

The sidebar swaps out the drive listing for the chats in the backup, bucketed by date the way the ChatGPT sidebar itself
groups them. Selecting a chat renders the whole thread in the reading pane, code highlighting and all. Only the branch
you actually left the conversation on is shown, so edited prompts and regenerated answers don't show up twice.

*File → Close Backup* puts the drive listing back. The backup you had open is remembered, so MDGlance reopens it next
time you start it.

Only the ChatGPT export format is understood for now.

## Build

Any Visual Studio Edition including Professional, Community, Express, etc. released in the last decade can be used to build the solution.

## Compatibility

- Runs on Windows 7 and later as long as .NET Framework 4 or above is installed.
- Should run on Linux too through WINE compatibility layer.