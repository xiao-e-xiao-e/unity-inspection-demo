package httpapi

import (
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"path/filepath"
	"strings"
	"testing"

	"inspection/server/internal/model"
	"inspection/server/internal/storage"
)

const valid = `{"title":"  机柜异常  ","description":"温度偏高","priority":"high","position":{"x":1.25,"y":0.8,"z":-2.4}}`

func call(h http.Handler, method, path, body string) *httptest.ResponseRecorder {
	r := httptest.NewRequest(method, path, strings.NewReader(body))
	if body != "" {
		r.Header.Set("Content-Type", "application/json")
	}
	w := httptest.NewRecorder()
	h.ServeHTTP(w, r)
	return w
}
func setup(t *testing.T) (http.Handler, *storage.Store) {
	t.Helper()
	s, err := storage.Open(filepath.Join(t.TempDir(), "test.db"))
	if err != nil {
		t.Fatal(err)
	}
	t.Cleanup(func() { s.Close() })
	return New(s, []string{"http://localhost:5173"}), s
}

func TestLifecycleAndPersistence(t *testing.T) {
	path := filepath.Join(t.TempDir(), "issues.db")
	s, err := storage.Open(path)
	if err != nil {
		t.Fatal(err)
	}
	h := New(s, nil)
	if w := call(h, "GET", "/api/health", ""); w.Code != 200 {
		t.Fatal(w.Body.String())
	}
	if w := call(h, "GET", "/api/issues", ""); strings.TrimSpace(w.Body.String()) != `{"items":[]}` {
		t.Fatal(w.Body.String())
	}
	w := call(h, "POST", "/api/issues", valid)
	if w.Code != 201 {
		t.Fatalf("%d %s", w.Code, w.Body.String())
	}
	var created model.Issue
	json.Unmarshal(w.Body.Bytes(), &created)
	if created.ID == "" || created.Status != "open" || created.Title != "机柜异常" || created.Position.Z != -2.4 || created.CreatedAt == "" || created.CreatedAt != created.UpdatedAt {
		t.Fatalf("unexpected record: %+v", created)
	}
	w = call(h, "POST", "/api/issues", valid)
	var second model.Issue
	json.Unmarshal(w.Body.Bytes(), &second)
	if second.ID == created.ID {
		t.Fatal("duplicate ID")
	}
	for _, status := range []string{"in_progress", "resolved", "open", "resolved"} {
		w = call(h, "PATCH", "/api/issues/"+created.ID+"/status", `{"status":"`+status+`"}`)
		if w.Code != 200 {
			t.Fatal(w.Body.String())
		}
	}
	var updated model.Issue
	json.Unmarshal(w.Body.Bytes(), &updated)
	if updated.CreatedAt != created.CreatedAt || updated.UpdatedAt == created.UpdatedAt {
		t.Fatal("timestamps incorrect")
	}
	if err := s.Close(); err != nil {
		t.Fatal(err)
	}
	s, err = storage.Open(path)
	if err != nil {
		t.Fatal(err)
	}
	defer s.Close()
	w = call(New(s, nil), "GET", "/api/issues", "")
	var list struct {
		Items []model.Issue `json:"items"`
	}
	json.Unmarshal(w.Body.Bytes(), &list)
	if len(list.Items) != 2 {
		t.Fatalf("lost records: %s", w.Body.String())
	}
	found := false
	for _, i := range list.Items {
		if i.ID == created.ID {
			found = true
			if i.Status != "resolved" || i.Position != created.Position || i.Description != created.Description {
				t.Fatalf("lost values: %+v", i)
			}
		}
	}
	if !found {
		t.Fatal("original ID missing")
	}
}

func TestValidationDoesNotWrite(t *testing.T) {
	h, _ := setup(t)
	cases := []struct{ name, body string }{
		{"blank", strings.Replace(valid, "  机柜异常  ", "   ", 1)},
		{"priority", strings.Replace(valid, "high", "urgent", 1)},
		{"missing coordinate", `{"title":"test","priority":"low","position":{"x":0,"y":0}}`},
		{"null coordinate", `{"title":"test","priority":"low","position":{"x":0,"y":0,"z":null}}`},
		{"null position", `{"title":"test","priority":"low","position":null}`},
		{"missing priority", `{"title":"test","position":{"x":0,"y":0,"z":0}}`},
		{"malformed", `{"title":`},
		{"null body", `null`},
		{"wrong type", strings.Replace(valid, "1.25", `"oops"`, 1)},
		{"overflow", strings.Replace(valid, "1.25", `1e999`, 1)},
		{"extra body", valid + valid},
		{"server field", strings.Replace(valid, `"title"`, `"status":"resolved","title"`, 1)},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			w := call(h, "POST", "/api/issues", tc.body)
			if w.Code != 400 {
				t.Fatalf("%d %s", w.Code, w.Body.String())
			}
			if !strings.Contains(w.Body.String(), `"error"`) {
				t.Fatal("missing error")
			}
		})
	}
	if w := call(h, "GET", "/api/issues", ""); strings.TrimSpace(w.Body.String()) != `{"items":[]}` {
		t.Fatal("invalid request persisted")
	}
	zero := `{"title":"零点","priority":"low","position":{"x":0,"y":0,"z":0}}`
	if w := call(h, "POST", "/api/issues", zero); w.Code != 201 {
		t.Fatal("zero coordinates should be allowed")
	}
}

func TestStatusErrorsAndCORS(t *testing.T) {
	h, _ := setup(t)
	w := call(h, "POST", "/api/issues", valid)
	var i model.Issue
	json.Unmarshal(w.Body.Bytes(), &i)
	if w = call(h, "PATCH", "/api/issues/"+i.ID+"/status", `{"status":"closed"}`); w.Code != 400 {
		t.Fatal(w.Code)
	}
	if w = call(h, "PATCH", "/api/issues/missing/status", `{"status":"resolved"}`); w.Code != 404 {
		t.Fatal(w.Code)
	}
	for _, method := range []string{"OPTIONS", "GET"} {
		r := httptest.NewRequest(method, "/api/issues", nil)
		r.Header.Set("Origin", "http://localhost:5173")
		r.Header.Set("Access-Control-Request-Method", "PATCH")
		r.Header.Set("Access-Control-Request-Headers", "content-type")
		w = httptest.NewRecorder()
		h.ServeHTTP(w, r)
		if w.Header().Get("Access-Control-Allow-Origin") != "http://localhost:5173" || !strings.Contains(w.Header().Get("Access-Control-Allow-Methods"), "PATCH") {
			t.Fatal("missing CORS headers")
		}
		if method == "OPTIONS" && w.Code != 204 {
			t.Fatal(w.Code)
		}
	}
	r := httptest.NewRequest("POST", "/api/issues", strings.NewReader(valid))
	r.Header.Set("Origin", "http://unapproved.example")
	r.Header.Set("Content-Type", "application/json")
	w = httptest.NewRecorder()
	h.ServeHTTP(w, r)
	if w.Code != 403 {
		t.Fatal(w.Code)
	}
	w = call(h, "GET", "/api/issues", "")
	var list struct {
		Items []model.Issue `json:"items"`
	}
	json.Unmarshal(w.Body.Bytes(), &list)
	if len(list.Items) != 1 || list.Items[0].Status != "open" {
		t.Fatal("rejected request changed data")
	}
}

func TestMediaSizeAndDatabaseFailure(t *testing.T) {
	h, s := setup(t)
	r := httptest.NewRequest("POST", "/api/issues", strings.NewReader(valid))
	w := httptest.NewRecorder()
	h.ServeHTTP(w, r)
	if w.Code != 415 {
		t.Fatal(w.Code)
	}
	w = call(h, "POST", "/api/issues", `{"title":"`+strings.Repeat("x", 70*1024)+`"}`)
	if w.Code != 413 {
		t.Fatal(w.Code)
	}
	s.Close()
	for _, tc := range []struct {
		method, path, body string
		code               int
	}{{"GET", "/api/health", "", 503}, {"GET", "/api/issues", "", 500}, {"POST", "/api/issues", valid, 500}} {
		w = call(h, tc.method, tc.path, tc.body)
		if w.Code != tc.code {
			t.Fatalf("%s: %d", tc.path, w.Code)
		}
	}
}
