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

This structure is shared across most Linux distributions, making your knowledge portable wherever you use Linux!
