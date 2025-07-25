# Key Ubuntu/Linux Directories at a Glance

Linux systems like Ubuntu use a standard directory structure called the Filesystem Hierarchy Standard (FHS). Each directory has a specific purpose, making it easier to manage and understand your system. Here’s an overview of the most important directories, what they’re for, and examples of what they contain.

| Directory   | Purpose & Usage                                                                 | Example Contents                |
|-------------|--------------------------------------------------------------------------------|---------------------------------|
| `/etc`      | System-wide configuration files for settings, users, networking, and more.      | `passwd`, `fstab`, `hostname`, `ssh/` |
| `/bin`      | Essential command binaries for all users, needed for basic system operation.    | `ls`, `bash`, `cp`, `mv`        |
| `/sbin`     | System administration binaries, mainly for root or advanced management.         | `reboot`, `init`, `ifconfig`    |
| `/usr`      | User programs, libraries, documentation, and shared resources.                  | `bin/`, `lib/`, `share/`        |
| `/var`      | Variable data files like logs, mail spools, caches, and temporary files.        | `log/`, `spool/`, `tmp/`        |
| `/home`     | Home directories for each regular user; personal files and settings.            | `username/`                     |
| `/root`     | Home directory for the root (administrator) user.                              | (root user files)               |
| `/lib`, `/lib64` | Essential shared libraries and kernel modules for programs in `/bin` and `/sbin`. | `modules/`                      |
| `/tmp`      | Temporary files for applications and the system, often deleted on reboot.       | (temporary files)               |
| `/dev`      | Device files representing hardware and virtual devices.                         | `sda`, `null`, `tty`            |
| `/proc`     | Virtual filesystem providing process and kernel information in real time.       | `cpuinfo`, `meminfo`, `[pid]/`  |
| `/mnt`      | Temporary mount point for filesystems, manually mounted by the user/admin.      | (mount directories)             |
| `/media`    | Mount points for removable media like USB drives and CDs, automatically handled.| (usb, cdrom directories)        |

---

## Directory Details

- **/etc:**  
  Stores configuration files for the entire system. For example, `/etc/passwd` lists user accounts; `/etc/fstab` configures disk mounts; `/etc/ssh/` holds SSH settings.

- **/bin & /sbin:**  
  `/bin` contains essential commands for all users (like `ls` and `cp`). `/sbin` has system management commands, usually run by root, such as `reboot` and `ifconfig`.

- **/usr:**  
  Houses most installed user programs and their libraries. `/usr/bin/` is for user commands, `/usr/lib/` for libraries, and `/usr/share/` for documentation.

- **/var:**  
  Contains files that change frequently, such as logs (`/var/log/`), mail (`/var/mail/`), and temporary files (`/var/tmp/`).

- **/home:**  
  Each user has a directory (like `/home/alice/`) for personal files, downloads, and user-specific configuration.

- **/root:**  
  The root user’s personal directory, separate from normal users.

- **/lib & /lib64:**  
  Shared libraries needed by programs in `/bin` and `/sbin`, plus kernel modules in `/lib/modules/`.

- **/tmp:**  
  A space for temporary files. Many programs use this directory for short-lived storage.

- **/dev:**  
  Provides access to devices (hard drives, terminals, etc.) as files. For example, `/dev/sda` is a hard disk, `/dev/null` is a “black hole” for data.

- **/proc:**  
  Not a real directory, but a virtual one exposing kernel and process info. Files like `/proc/cpuinfo` give hardware details; `/proc/[pid]/` has info on running processes.

- **/mnt & /media:**  
  `/mnt` is used for temporary manual mounts (e.g., mounting an external disk for maintenance). `/media` is used for automatic mounting of removable media by the system.

---

## Quick Command Example

When you type a command like `ls`, the shell finds and runs `/bin/ls` (unless another version exists earlier in your PATH).  
You can check the location with:
```bash
which ls
```

---

## Why These Directories Matter

Knowing these directories helps you:
- Troubleshoot system problems
- Manage users and configuration
- Find logs and temporary files
- Understand where programs and libraries are installed
- Safely mount/unmount drives and devices

# The `ps` Command in Linux
`ps` lets you view running processes.

| Command      | What it Shows                                           |
|--------------|--------------------------------------------------------|
| `ps`         | Processes in your current terminal/session              |
| `ps -A`      | All processes on the system                             |
| `ps -Af`     | All processes, with full details (user, PID, command…)  |
| `ps -u user` | All processes for a specific user                       |

- Use `ps -Af` for a full, detailed list of all system processes.

# Understanding Groups in Linux

## What is a Group?

A **group** in Linux is a way to organize users so they can share permissions and access to files, directories, and system resources.  
Think of a group as a "team"—users in the same group can be given common rights and can collaborate on shared files.

### Why Use Groups?
- **Simplifies permissions management:** Instead of giving permissions to each user individually, you can give them to a group.
- **Facilitates collaboration:** Multiple users can work on the same files if they belong to the same group.

---

## How Groups Work

- Each user can belong to one **primary group** and to multiple **secondary (supplementary) groups**.
- Files and directories have an owner and a group associated with them.
- Group membership determines what actions (read, write, execute) a user can perform on files belonging to that group.

**Example:**  
If a file is owned by group `staff` and you are in the `staff` group, you can access it according to the group's permissions.

### Types of Groups

- **System Groups:** Used by the operating system for specific services (`man`, `adm`, `syslog`, etc.).
- **User Groups:** Created for individual users or teams. By default, each user gets a group with their own name.

---

## `/etc/group` File Explained

This file lists all groups and their members:

```
groupname:x:GID:user1,user2,...
```
- `groupname`: The name of the group (e.g., `man`, `adm`, `ehsan`)
- `x`: Password placeholder (almost always `x`)
- `GID`: Group ID (unique number for each group)
- `user1,user2,...`: Users who are members of the group (comma-separated, optional)

### Example Breakdown

```
man:x:12:
adm:x:4:syslog,ehsan
ehsan:x:1000:
```
- **man**: System group for manual pages. No users listed.
- **adm**: System group for administrative tasks. Members: `syslog`, `ehsan`.
- **ehsan**: User group, usually created for the user `ehsan`. Sometimes users have their own private group with the same name.

---

## Why Is My Name a Group?

- When you create a user (`ehsan`), Linux also creates a group called `ehsan` by default. This is called a "user private group."
- Your user account will be a member of your own group (`ehsan`), and you can also be added to other groups for extra permissions.

---

## Common Default Groups on a Fresh Linux Installation

Here are some of the most important groups you’ll see by default in `/etc/group`:

| Group   | Purpose                                                    |
|---------|------------------------------------------------------------|
| `root`  | Superuser/administrator                                    |
| `users` | All regular users (not always present in all distributions)|
| `adm`   | System monitoring and log access                           |
| `sudo`  | Users allowed to run commands as root (via `sudo`)         |
| `wheel` | Similar to `sudo`; on some distros, gives root access      |
| `staff` | General-purpose group for staff users                      |
| `man`   | Access to manual pages                                     |
| `lp`    | Printing system                                            |
| `mail`  | Mail server access                                         |
| `sys`   | System files and resources                                 |
| `tty`   | Terminal access                                            |
| `disk`  | Direct disk access                                         |
| `audio` | Sound hardware access                                      |
| `video` | Video hardware access                                      |
| `cdrom` | CD-ROM access                                              |
| `plugdev`| Hotplug devices (USB, etc.)                              |
| `ssh`   | SSH access (not always default)                            |

> Most groups are used by the system to manage permissions for hardware, services, and administration.  
> You may also have a group named after your username, which is your private user group.

---

## Summary Table

| Concept        | Description                                                      |
|----------------|------------------------------------------------------------------|
| Group          | Collection of users sharing access rights                        |
| Primary Group  | Default group for a user                                         |
| Secondary Group| Additional groups a user can be a member of                      |
| System Group   | Used by OS/services (e.g. `man`, `adm`, `syslog`)                |
| User Group     | Created for users (e.g. `ehsan`)                                 |
| `/etc/group`   | File listing all groups and their members                        |
| Default Groups | Groups created by the system for core functions                  |

---

**In practice:**  
- Groups let you manage permissions efficiently.
- Your name as a group is normal—every user gets a private group.
- You can be a member of many groups, gaining different rights in each.
- Default groups help the system organize access for services, hardware, and administration.
