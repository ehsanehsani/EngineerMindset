# Git

Interview questions. Add more here later.

---

# How do commits work in Git? What is a parent?

A commit is a **snapshot** + a **pointer to its parent**. Follow that pointer, then the next, all the way to the first commit. That chain is history.

```
A ← B ← C ← D     (main / HEAD)
```

`D`’s parent is `C`, `C`’s is `B`, `A` has none (the root).

The commit hash **includes the parent id**. Change the parent → **new hash**. Git does not edit a commit; it creates a new one.

A **branch** is only a label on a commit. Normal commits have **one** parent. A **merge commit** has **two**.

---

# What is the difference between merge and rebase?

You branched, `main` moved on:

```
A -- B -- C          ← main
      \
       D -- E        ← feature
```

**Merge** — keep both histories. Add a new commit `M` with **two parents** (`C` and `E`). `D` and `E` stay the same.

```
A -- B -- C ------ M
      \           /
       D ------- E
```

**Rebase** — you do **not** change `C`. `main` stays `A -- B -- C`.

You started `feature` from `B`. Then someone added `C` on `main`. Rebase means: *replay my work as if I had branched from `C` instead of from `B`.* So your commits are **appended after** `C`, at the end of the line.

```
A -- B -- C -- D' -- E'     ← feature (your work, last)
          ↑
        untouched
```

Why the end? Git applies the diff of `D` on top of `C` → that new snapshot is `D'`. Then it applies the diff of `E` on top of `D'` → `E'`. Parent of `D'` is `C` (new hash). `C` itself is never rewritten.

Don’t rebase commits **other people already pulled** (new hashes; their history breaks). Rebase your local feature branch; merge into `main`.

**Interview line:** Commits point at a parent, back to the first commit. Merge = extra commit with two parents. Rebase = copy your commits and point them at a new parent.
