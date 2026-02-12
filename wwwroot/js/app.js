const { useEffect, useMemo, useState } = React;

const statuses = ["Wishlist", "Applied", "Interviewing", "Offer", "Rejected", "Ghosted", "Closed"];
const apiBase = "/api/applications";
const usStates = [
  "Alabama", "Alaska", "Arizona", "Arkansas", "California", "Colorado", "Connecticut", "Delaware",
  "Florida", "Georgia", "Hawaii", "Idaho", "Illinois", "Indiana", "Iowa", "Kansas", "Kentucky",
  "Louisiana", "Maine", "Maryland", "Massachusetts", "Michigan", "Minnesota", "Mississippi",
  "Missouri", "Montana", "Nebraska", "Nevada", "New Hampshire", "New Jersey", "New Mexico",
  "New York", "North Carolina", "North Dakota", "Ohio", "Oklahoma", "Oregon", "Pennsylvania",
  "Rhode Island", "South Carolina", "South Dakota", "Tennessee", "Texas", "Utah", "Vermont",
  "Virginia", "Washington", "West Virginia", "Wisconsin", "Wyoming",
];

const savedFilters = [
  {
    key: "applied14",
    label: "Applied last 14 days",
    predicate: (x) => x.status === "Applied" && daysSince(x.appliedOn) <= 14,
  },
  {
    key: "interviewing",
    label: "Interviewing",
    predicate: (x) => x.status === "Interviewing",
  },
  {
    key: "followup_due",
    label: "Follow-up due",
    predicate: (x) => x.followUpDate && x.followUpDate <= today(),
  },
  {
    key: "ghosted",
    label: "Ghosted",
    predicate: (x) => x.status === "Ghosted",
  },
];

function App() {
  const [items, setItems] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [globalSearch, setGlobalSearch] = useState("");
  const [activeSavedFilter, setActiveSavedFilter] = useState("");
  const [quickAddOpen, setQuickAddOpen] = useState(false);
  const [paletteOpen, setPaletteOpen] = useState(false);
  const [paletteMode, setPaletteMode] = useState("root");
  const [paletteAppId, setPaletteAppId] = useState("");
  const [paletteStatus, setPaletteStatus] = useState("Applied");
  const [paletteDate, setPaletteDate] = useState(today());
  const [inlineEdits, setInlineEdits] = useState({});
  const [quickAdd, setQuickAdd] = useState({
    company: "",
    role: "",
    status: "Applied",
    appliedOn: today(),
    location: "",
    followUpDate: "",
  });

  async function loadData() {
    setLoading(true);
    setError("");
    try {
      const response = await fetch(apiBase);
      if (!response.ok) throw new Error(`Request failed (${response.status})`);
      setItems(await response.json());
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    loadData();
  }, []);

  useEffect(() => {
    function onKeydown(e) {
      const tag = (document.activeElement?.tagName || "").toLowerCase();
      const inTextInput = tag === "input" || tag === "textarea" || document.activeElement?.isContentEditable;
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setPaletteOpen((x) => !x);
        setPaletteMode("root");
      } else if (!inTextInput && e.key.toLowerCase() === "n") {
        e.preventDefault();
        setQuickAddOpen(true);
      } else if (!inTextInput && e.key === "/") {
        e.preventDefault();
        const el = document.getElementById("global-search");
        if (el) el.focus();
      } else if (e.key === "Escape") {
        setQuickAddOpen(false);
        setPaletteOpen(false);
      }
    }

    window.addEventListener("keydown", onKeydown);
    return () => window.removeEventListener("keydown", onKeydown);
  }, []);

  const filtered = useMemo(() => {
    let out = [...items];
    if (globalSearch.trim()) {
      const q = globalSearch.trim().toLowerCase();
      out = out.filter((x) =>
        [x.company, x.role, x.status, x.location, x.notes, x.salaryText, (x.keySkills || []).join(" ")]
          .filter(Boolean)
          .join(" ")
          .toLowerCase()
          .includes(q)
      );
    }

    if (activeSavedFilter) {
      const f = savedFilters.find((x) => x.key === activeSavedFilter);
      if (f) out = out.filter(f.predicate);
    }

    return out;
  }, [items, globalSearch, activeSavedFilter]);

  const grouped = useMemo(() => {
    return statuses.reduce((acc, s) => {
      acc[s] = filtered.filter((x) => x.status === s);
      return acc;
    }, {});
  }, [filtered]);

  async function createQuickAdd(e) {
    e.preventDefault();
    setError("");
    if (!quickAdd.company.trim() || !quickAdd.role.trim()) {
      setError("Company and role are required.");
      return;
    }

    const response = await fetch(apiBase, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        company: quickAdd.company.trim(),
        role: quickAdd.role.trim(),
        status: quickAdd.status,
        appliedOn: quickAdd.appliedOn,
        location: quickAdd.location || null,
        followUpDate: quickAdd.followUpDate || null,
      }),
    });

    if (!response.ok) {
      setError(await response.text());
      return;
    }

    setQuickAdd({ company: "", role: "", status: "Applied", appliedOn: today(), location: "", followUpDate: "" });
    setQuickAddOpen(false);
    await loadData();
  }

  function startInlineEdit(item) {
    setInlineEdits({
      ...inlineEdits,
      [item.id]: {
        role: item.role,
        status: item.status,
        location: item.location || "",
        followUpDate: item.followUpDate || "",
        notes: item.notes || "",
      },
    });
  }

  function cancelInlineEdit(id) {
    const next = { ...inlineEdits };
    delete next[id];
    setInlineEdits(next);
  }

  function updateInlineField(id, key, value) {
    setInlineEdits({
      ...inlineEdits,
      [id]: { ...inlineEdits[id], [key]: value },
    });
  }

  async function saveInlineEdit(item) {
    const draft = inlineEdits[item.id];
    if (!draft) return;
    const payload = {
      company: item.company,
      role: draft.role,
      status: draft.status,
      appliedOn: item.appliedOn,
      followUpDate: draft.followUpDate || null,
      jobUrl: item.jobUrl || null,
      applicationLink: item.applicationLink || null,
      location: draft.location || null,
      jobLevel: item.jobLevel || null,
      salaryText: item.salaryText || null,
      keySkills: item.keySkills || [],
      sourceUrl: item.sourceUrl || null,
      notes: draft.notes || null,
    };

    const response = await fetch(`${apiBase}/${item.id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });

    if (!response.ok) {
      setError(await response.text());
      return;
    }

    cancelInlineEdit(item.id);
    await loadData();
  }

  async function deleteItem(id) {
    if (!confirm("Delete this application?")) return;
    const response = await fetch(`${apiBase}/${id}`, { method: "DELETE" });
    if (!response.ok) {
      setError(`Delete failed (${response.status})`);
      return;
    }
    await loadData();
  }

  async function applyPaletteAction(action) {
    if (action === "add") {
      setQuickAddOpen(true);
      setPaletteOpen(false);
      return;
    }
    if (action === "status") {
      setPaletteMode("status");
      return;
    }
    if (action === "followup") {
      setPaletteMode("followup");
      return;
    }
  }

  async function runPaletteStatusChange() {
    const item = items.find((x) => String(x.id) === paletteAppId);
    if (!item) {
      setError("Select an application in command palette.");
      return;
    }
    const response = await fetch(`${apiBase}/${item.id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        company: item.company,
        role: item.role,
        status: paletteStatus,
        appliedOn: item.appliedOn,
        followUpDate: item.followUpDate || null,
        jobUrl: item.jobUrl || null,
        applicationLink: item.applicationLink || null,
        location: item.location || null,
        jobLevel: item.jobLevel || null,
        salaryText: item.salaryText || null,
        keySkills: item.keySkills || [],
        sourceUrl: item.sourceUrl || null,
        notes: item.notes || null,
      }),
    });
    if (!response.ok) {
      setError(await response.text());
      return;
    }
    setPaletteOpen(false);
    setPaletteMode("root");
    setPaletteAppId("");
    await loadData();
  }

  async function runPaletteFollowUp() {
    const item = items.find((x) => String(x.id) === paletteAppId);
    if (!item) {
      setError("Select an application in command palette.");
      return;
    }
    const response = await fetch(`${apiBase}/${item.id}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        company: item.company,
        role: item.role,
        status: item.status,
        appliedOn: item.appliedOn,
        followUpDate: paletteDate || null,
        jobUrl: item.jobUrl || null,
        applicationLink: item.applicationLink || null,
        location: item.location || null,
        jobLevel: item.jobLevel || null,
        salaryText: item.salaryText || null,
        keySkills: item.keySkills || [],
        sourceUrl: item.sourceUrl || null,
        notes: item.notes || null,
      }),
    });
    if (!response.ok) {
      setError(await response.text());
      return;
    }
    setPaletteOpen(false);
    setPaletteMode("root");
    setPaletteAppId("");
    await loadData();
  }

  return (
    <div className="page">
      <header className="hero">
        <h1>Job Application Tracker</h1>
        <p>Ctrl+K command palette, N quick add, / focus search</p>
      </header>

      <section className="panel command-bar">
        <input
          id="global-search"
          placeholder="Global search by company, title, status, skills, notes..."
          value={globalSearch}
          onChange={(e) => setGlobalSearch(e.target.value)}
        />
        <button type="button" onClick={() => setQuickAddOpen(true)}>Quick Add (N)</button>
        <button type="button" className="ghost" onClick={() => setPaletteOpen(true)}>Command Palette (Ctrl+K)</button>
      </section>

      <section className="panel saved-filters">
        <strong>Saved filters:</strong>
        <div className="chip-row">
          <button
            type="button"
            className={!activeSavedFilter ? "chip active" : "chip"}
            onClick={() => setActiveSavedFilter("")}
          >
            All
          </button>
          {savedFilters.map((f) => (
            <button
              key={f.key}
              type="button"
              className={activeSavedFilter === f.key ? "chip active" : "chip"}
              onClick={() => setActiveSavedFilter(f.key)}
            >
              {f.label}
            </button>
          ))}
        </div>
      </section>

      {error && <div className="panel error">{error}</div>}
      {loading && <div className="panel">Loading...</div>}

      {!loading && (
        <section className="pipeline">
          {statuses.map((s) => (
            <div key={s} className="column">
              <h3>{s} <span>{grouped[s]?.length ?? 0}</span></h3>
              <div className="cards">
                {(grouped[s] || []).map((item) => {
                  const edit = inlineEdits[item.id];
                  return (
                    <article key={item.id} className="card">
                      <strong>{item.company}</strong>
                      {!edit ? (
                        <>
                          <p>{item.role}</p>
                          <small>{item.location || "Location n/a"}</small>
                          <small>Applied: {item.appliedOn}</small>
                          {item.followUpDate && <small className={isDue(item.followUpDate) ? "due" : ""}>Follow-up: {item.followUpDate}</small>}
                          <div className="card-actions">
                            <button type="button" onClick={() => startInlineEdit(item)}>Inline Edit</button>
                            <button type="button" className="danger" onClick={() => deleteItem(item.id)}>Delete</button>
                          </div>
                        </>
                      ) : (
                        <div className="inline-edit">
                          <input
                            value={edit.role}
                            onChange={(e) => updateInlineField(item.id, "role", e.target.value)}
                            placeholder="Role"
                          />
                          <select
                            value={edit.status}
                            onChange={(e) => updateInlineField(item.id, "status", e.target.value)}
                          >
                            {statuses.map((x) => <option key={x} value={x}>{x}</option>)}
                          </select>
                          <select
                            value={edit.location}
                            onChange={(e) => updateInlineField(item.id, "location", e.target.value)}
                          >
                            <option value="">Select state</option>
                            {usStates.map((state) => <option key={state} value={state}>{state}</option>)}
                          </select>
                          <input
                            type="date"
                            value={edit.followUpDate}
                            onChange={(e) => updateInlineField(item.id, "followUpDate", e.target.value)}
                          />
                          <textarea
                            rows="2"
                            value={edit.notes}
                            onChange={(e) => updateInlineField(item.id, "notes", e.target.value)}
                            placeholder="Notes"
                          />
                          <div className="card-actions">
                            <button type="button" onClick={() => saveInlineEdit(item)}>Save</button>
                            <button type="button" className="ghost" onClick={() => cancelInlineEdit(item.id)}>Cancel</button>
                          </div>
                        </div>
                      )}
                    </article>
                  );
                })}
              </div>
            </div>
          ))}
        </section>
      )}

      {quickAddOpen && (
        <div className="overlay" onClick={() => setQuickAddOpen(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h3>Quick Add Application</h3>
            <form onSubmit={createQuickAdd} className="quick-add-form">
              <input
                autoFocus
                placeholder="Company"
                value={quickAdd.company}
                onChange={(e) => setQuickAdd({ ...quickAdd, company: e.target.value })}
              />
              <input
                placeholder="Role"
                value={quickAdd.role}
                onChange={(e) => setQuickAdd({ ...quickAdd, role: e.target.value })}
              />
              <select
                value={quickAdd.status}
                onChange={(e) => setQuickAdd({ ...quickAdd, status: e.target.value })}
              >
                {statuses.map((s) => <option key={s} value={s}>{s}</option>)}
              </select>
              <select
                value={quickAdd.location}
                onChange={(e) => setQuickAdd({ ...quickAdd, location: e.target.value })}
              >
                <option value="">Select state</option>
                {usStates.map((state) => <option key={state} value={state}>{state}</option>)}
              </select>
              <input
                type="date"
                value={quickAdd.appliedOn}
                onChange={(e) => setQuickAdd({ ...quickAdd, appliedOn: e.target.value })}
              />
              <input
                type="date"
                value={quickAdd.followUpDate}
                onChange={(e) => setQuickAdd({ ...quickAdd, followUpDate: e.target.value })}
              />
              <div className="actions">
                <button type="submit">Add</button>
                <button type="button" className="ghost" onClick={() => setQuickAddOpen(false)}>Close</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {paletteOpen && (
        <div className="overlay" onClick={() => setPaletteOpen(false)}>
          <div className="palette" onClick={(e) => e.stopPropagation()}>
            {paletteMode === "root" && (
              <>
                <h3>Command Palette</h3>
                <button type="button" onClick={() => applyPaletteAction("add")}>Add app</button>
                <button type="button" onClick={() => applyPaletteAction("status")}>Change status</button>
                <button type="button" onClick={() => applyPaletteAction("followup")}>Schedule follow-up</button>
              </>
            )}
            {paletteMode === "status" && (
              <>
                <h3>Change Status</h3>
                <select value={paletteAppId} onChange={(e) => setPaletteAppId(e.target.value)}>
                  <option value="">Select application</option>
                  {items.map((x) => <option key={x.id} value={x.id}>{x.company} - {x.role}</option>)}
                </select>
                <select value={paletteStatus} onChange={(e) => setPaletteStatus(e.target.value)}>
                  {statuses.map((s) => <option key={s} value={s}>{s}</option>)}
                </select>
                <div className="actions">
                  <button type="button" onClick={runPaletteStatusChange}>Apply</button>
                  <button type="button" className="ghost" onClick={() => setPaletteMode("root")}>Back</button>
                </div>
              </>
            )}
            {paletteMode === "followup" && (
              <>
                <h3>Schedule Follow-Up</h3>
                <select value={paletteAppId} onChange={(e) => setPaletteAppId(e.target.value)}>
                  <option value="">Select application</option>
                  {items.map((x) => <option key={x.id} value={x.id}>{x.company} - {x.role}</option>)}
                </select>
                <input type="date" value={paletteDate} onChange={(e) => setPaletteDate(e.target.value)} />
                <div className="actions">
                  <button type="button" onClick={runPaletteFollowUp}>Set</button>
                  <button type="button" className="ghost" onClick={() => setPaletteMode("root")}>Back</button>
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

function today() {
  return new Date().toISOString().slice(0, 10);
}

function isDue(dateText) {
  return dateText <= today();
}

function daysSince(dateText) {
  const now = new Date();
  const then = new Date(dateText);
  return Math.floor((now - then) / 86400000);
}

ReactDOM.createRoot(document.getElementById("root")).render(<App />);
