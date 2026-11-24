---
description: Informal technical register for documents that teach developers, such as a manual or a guide
keep-coding-instructions: true
---

This file is the same for every repository of the organization. A fact specific to one repository lives in a rule file.

The "team" this file refers to means a repository's maintainers and collaborators. In private projects, it may also refer to Tenacom as a whole.

## Scope

The rules below define an informal technical register. It is the register of a document that teaches: a manual, a guide, a tutorial. The reader is a developer who reads the document in order, from the first page.

Claude Code loads only the style that `settings.json` selects, and this style is not that one. It applies to the text of an informal document, as "Register" in `rules/common/documentation.md` defines it. Read this file before you write or review an informal document. Everything else in the session keeps the selected style: the chat, the commit messages, the code comments, the issues, and the pull requests.

A document can have conventions of its own: a terminology, a set of examples, an exception a reviewer accepted. They win over the rules below.

## Exemptions

Reproduce verbatim, without applying these rules: code, identifiers, error text, command output, and quoted file contents.

## Informal is the manner, not the content

- Every statement is as correct and as precise as in a reference. The register changes how a rule is explained. It never changes what the rule says.
- Use the same term for the same concept every time. A synonym makes the reader look for a second concept.
- Introduce a term before you use it. Put a prerequisite before what depends on it. A reader who stops on a sentence must understand everything up to that sentence.
- Start from the basics, with an example for every concept. Readability counts more than a formally complete exposition.

## Voice

Three grammatical persons, each with one job.

| Person | Job                                                                                     | Examples                                                                                                                                       |
| :----- | :-------------------------------------------------------------------------------------- | :--------------------------------------------------------------------------------------------------------------------------------------------- |
| «you»  | the reader                                                                              | «This guide is for you, if you have to...», «The navigation bar will lead you to the right place», «If you stumble into a term you don't know» |
| «we»   | the technical choices of the team, and the reasoning that author and reader do together | «Let's try and make it async», «we adopt a good practice», «Let's suppose we have to...»                                                       |
| «I»    | the author, about the writing of the document itself                                    | «I'm quoting Microsoft's documentation here», «I prefer bulleted lists», «I'm taking it for granted», «I won't delve into it for now»          |

- Never the reader in the third person: no «the reader», no «this guide requires previous knowledge of». A «we» for the author alone reads as a plural of majesty.
- A rule names the thing it governs, or takes the imperative: «The version number must be incremented», «Never block on async code». It never takes «we» or «I».
- A choice of the team appears as «we do this and that», with its reason: «In practice, we are keeping some projects on .NET Framework because of legacy dependencies».
- A rhetorical question makes a transition: «So, how do we dispose all of them properly?».
- A short sentence closes a reasoning: «Easy: there's a method for that.», «`Dispose(void)` is enough.».

## Register

- Colloquial technical: the way an experienced developer explains something to a colleague.
- A light remark in a descriptive sentence is register, not humor: «This turns your CPU into an expensive egg cooker», «It works, until it doesn't, usually on a Saturday night.».

## Humor

- A joke goes in a footnote or in an alert box. It never goes in a sentence that states a rule.
- At most one joke per section.
- A joke is told once. It does not come back in a later section or in another chapter.
- The tone is dry and short, and the joke says something true about the trade: «Don't trust the client, even if you wrote it. _Especially_ if you wrote it.», «Just when you thought you've grokked async, you read a stack trace and weep.».

## Emphasis and marks

- **Bold** marks the key rule of a section: «**A resource must have a name**». One or two per section.
- _Italics_ mark the first occurrence of a term the document defines (_business domain_, _collection_), a foreign term (_chapeau!_), and an emphasis («_all unmanaged resources_»). After its definition, a term is set in roman.
- A code span holds whatever the reader types, or reads in code or on the wire: `GET /users`, `Dispose(bool)`, `lastIndex`.
- Double quotes hold informal names ("super-duper-updater") or mark irony («The alleged "high resolution" timer»).
- GitHub-style alerts are allowed. The context and the content pick the kind: `[!IMPORTANT]` for the rule the reader must not forget, `[!TIP]` for a piece of advice that is not a rule, `[!WARNING]` for a mistake with consequences. At most one box per section.
- A footnote holds an aside or a joke.
- A link in the text points to a source: a standard, an encyclopedia entry, an article. Not every citation needs a link.

## Lists

- A sentence that ends with a colon introduces a list.
- When the items are fragments, they start lowercase and end with `;`, and the last item ends with `.`. When the items are complete sentences, each starts with a capital and ends with `.`. Do not mix the two forms in one list.
- An item holds at most two sentences. It may hold a parenthesis with an example.
- Prefer a list to an enumeration inside a sentence.

## Size

- A section has two to five paragraphs.
- A paragraph has one to four sentences.
- These limits are tight on purpose. Do not go over them on your own judgement. The user accepts an exception case by case, and an accepted exception does not widen the limit.
