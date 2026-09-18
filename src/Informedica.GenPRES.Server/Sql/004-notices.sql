-- Migration 4 (plan 516 step 8): what a Session has in flight — the data notice it was told,
-- the signing challenge it holds, and the answer a Submission was given. Append-only like the
-- rest; all three live minutes and are dropped whole, never kept as unreadable entries.
--
-- Nothing drops them yet: a lifetime is read at load, so a row past it is already absent to
-- every request, but it stays on disk until the sweep that removes these three tables and the
-- launch rows exists (#516). The sweep is a statement of its own, never part of a request's
-- transaction, and never touches the session, credential or order_plan rows, which are kept.

-- Rule 44. The notice that the patient data could not be verified, one per Session, for two
-- minutes. The reading is the patient as the platform gave it, stored as its Dto under a
-- structure version; a release that cannot read it ends the Session, as an opened-with does.
create table data_notice (
    id           integer primary key,
    session_id   text not null references session (session_id),
    nonce        text not null,
    json_version integer null,       -- null: the platform had no reading to show
    data         text null,          -- json: Patient.Dto
    expiry       integer not null,
    at           integer not null,
    check ((data is null) = (json_version is null))
);
create index ix_data_notice on data_notice (session_id, id);

-- Rules 42 and 43. The challenge a signature answers: the digest of the order plan it was
-- issued over, never the plan itself, and the platform's reading at that moment, which decides
-- whether the version is signed as verified.
create table challenge (
    id           integer primary key,
    session_id   text not null references session (session_id),
    nonce        text not null,
    digest       text not null,
    json_version integer null,       -- null: the reading could not be taken
    reading      text null,          -- json: Patient.Dto
    expiry       integer not null,
    at           integer not null,
    check ((reading is null) = (json_version is null))
);
create index ix_challenge on challenge (session_id, id);

-- The challenge is spent: a commit used it, or a version was opened under it. A spent
-- challenge is never the live one again.
create table challenge_spent (
    challenge_id integer primary key references challenge (id),
    at           integer not null
);

-- Rule 45. What a Submission was answered, by Session and by the client's key, so that the
-- same Submission sent twice is answered once and the same way. The version and the head that
-- blocked are named by id: both are order_plan rows the answer can be rebuilt from.
create table submission_answer (
    session_id    text not null references session (session_id),
    idem_key      text not null,
    answer        text not null,     -- submitted | refused:<word>
    version_id    text null,         -- submitted: the version signed; blocked: the head
    opened_token  text null,         -- submitted: the token minted with it
    attempts_left integer null,      -- pin-wrong: the tries that remain
    locked_until  integer null,      -- locked: until when
    at            integer not null,
    primary key (session_id, idem_key)
);
