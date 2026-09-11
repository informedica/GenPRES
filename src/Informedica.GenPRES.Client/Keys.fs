/// <summary>
/// The browser key pair of the launch: a non-extractable ECDSA P-256 signing key made at the
/// launch, whose private key stays in IndexedDB under the public key's RFC 7638 thumbprint and
/// whose public key goes to the server with the Launch. The server stores the public key in
/// the SessionRecord and answers the thumbprint; <c>keep</c> then prunes the private keys of
/// earlier launches once they are older than the grace period. Signing every request with it
/// (launch step 7, DPoP) comes later; the key is made now so the SessionRecord already holds it.
/// </summary>
/// <remarks>
/// WebCrypto and IndexedDB are promise- and event-based browser APIs, so the interop is one
/// JavaScript literal (the pattern of <c>themeDef</c> in App.fs) with thin typed F# wrappers.
/// Keys are kept per thumbprint, not as a single entry, so a second launch in another tab
/// that is refused cannot take away the key of the first tab's open Session. Pruning is by
/// age, not by identity: <c>keep</c> deletes only keys older than the Launch lifetime, so two
/// launches racing in two tabs cannot delete each other's fresh key and leave
/// the winning Session unable to sign. The loser's key lingers until the next launch prunes it.
/// </remarks>
module Keys

open Fable.Core
open Fable.Core.JsInterop
open Shared.Types


[<Literal>]
let private keysDef =
    """
(() => {
    const DB = "genpres";
    const STORE = "keys";
    // no launch still in flight can own a key older than this, the Launch lifetime with a
    // margin; keys younger than
    // it are left alone by keep, whoever calls it
    const GRACE_MS = 10 * 60 * 1000;

    const b64url = bytes =>
        btoa(String.fromCharCode(...new Uint8Array(bytes)))
            .replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");

    // an IDBRequest as a promise
    const request = r => new Promise((resolve, reject) => {
        r.onsuccess = () => resolve(r.result);
        r.onerror = () => reject(r.error);
    });

    const openDb = () => new Promise((resolve, reject) => {
        const r = indexedDB.open(DB, 1);
        r.onupgradeneeded = () => r.result.createObjectStore(STORE);
        r.onsuccess = () => resolve(r.result);
        r.onerror = () => reject(r.error);
    });

    // run f over the key store in one transaction; the completion is awaited after f so
    // that an auto-committed transaction is still observed
    const withStore = async (mode, f) => {
        const db = await openDb();
        try {
            const tx = db.transaction(STORE, mode);
            const done = new Promise((resolve, reject) => {
                tx.oncomplete = () => resolve();
                tx.onerror = () => reject(tx.error);
                tx.onabort = () => reject(tx.error);
            });
            const result = await f(tx.objectStore(STORE));
            await done;
            return result;
        } finally {
            db.close();
        }
    };

    // RFC 7638: SHA-256 over the required members of an EC key, in lexicographic order,
    // without whitespace; base64url without padding. The server computes the same value
    // (ServerApi.Adapters.PublicKey.thumbprint).
    const thumbprint = async jwk => {
        const canonical = JSON.stringify({ crv: jwk.crv, kty: jwk.kty, x: jwk.x, y: jwk.y });
        return b64url(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(canonical)));
    };

    return {
        // a fresh pair; the private key is stored under the thumbprint; the public JWK text
        // is returned
        generate: async () => {
            const pair = await crypto.subtle.generateKey(
                { name: "ECDSA", namedCurve: "P-256" }, false, ["sign", "verify"]);
            const jwk = await crypto.subtle.exportKey("jwk", pair.publicKey);
            const id = await thumbprint(jwk);
            const entry = { key: pair.privateKey, createdAt: Date.now() };
            await withStore("readwrite", store => request(store.put(entry, id)));
            return JSON.stringify({ kty: jwk.kty, crv: jwk.crv, x: jwk.x, y: jwk.y });
        },
        thumbprint: json => thumbprint(JSON.parse(json)),
        // delete every stored key except this one and those younger than the grace period
        keep: id => withStore("readwrite", async store => {
            const cutoff = Date.now() - GRACE_MS;
            for (const other of await request(store.getAllKeys())) {
                if (other === id) continue;
                const entry = await request(store.get(other));
                if (!entry || !entry.createdAt || entry.createdAt < cutoff) await request(store.delete(other));
            }
        }),
        // the thumbprints of the stored keys
        list: () => withStore("readonly", store => request(store.getAllKeys()))
    };
})()
"""


// emitJsExpr, not an Emit attribute on a jsNative value: the attribute form is inlined at
// every use, which would run the literal's IIFE per call; this is evaluated once on load
let private keys: obj = emitJsExpr () keysDef


[<Emit("$0.then($1, $2)")>]
let private settle (promise: JS.Promise<'T>) (resolved: 'T -> unit) (rejected: obj -> unit) : unit = jsNative


/// A promise as an Async, so the callers fit Cmd.fromAsync; a rejection becomes an exception
/// carrying the rejection's text.
let private await (promise: JS.Promise<'T>) : Async<'T> =
    Async.FromContinuations(fun (cont, econt, _) ->
        settle promise cont (fun reason -> econt (System.Exception(string reason)))
    )


/// Creates the key pair of this launch: the private key, not extractable, goes into IndexedDB
/// under the public key's thumbprint; the public key comes back as JWK text.
let generate () : Async<PublicKey> =
    async {
        let! json = keys?generate () |> await
        return PublicKey json
    }


/// The RFC 7638 thumbprint of a public JWK, as the server computes it.
let thumbprint (PublicKey json) : Async<string> = keys?thumbprint json |> await


/// Deletes the stored private keys of earlier launches: every key except this one and those
/// younger than the Launch lifetime. Called when a Session opens with this key. A key made by
/// a launch racing in another tab is left alone; if that launch won, its key is the one its
/// Session signs with, and this one is pruned by a later launch.
let keep (thumbprint: string) : Async<unit> = keys?keep thumbprint |> await


/// The thumbprints of the stored private keys.
let list () : Async<string[]> = keys?list () |> await
