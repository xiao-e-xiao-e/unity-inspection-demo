package httpapi

import (
	"database/sql"
	"embed"
	"encoding/json"
	"errors"
	"io"
	"log"
	"math"
	"mime"
	"net/http"
	"strings"
	"unicode/utf8"

	"inspection/server/internal/model"
	"inspection/server/internal/storage"
)

//go:embed tester.html
var pages embed.FS

type API struct{ store *storage.Store }
type apiError struct {
	Code    string `json:"code"`
	Message string `json:"message"`
	Field   string `json:"field,omitempty"`
}

func write(w http.ResponseWriter, status int, value any) {
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	w.WriteHeader(status)
	if err := json.NewEncoder(w).Encode(value); err != nil {
		log.Printf("write response: %v", err)
	}
}
func fail(w http.ResponseWriter, status int, code, message, field string) {
	write(w, status, struct {
		Error apiError `json:"error"`
	}{apiError{code, message, field}})
}
func internal(w http.ResponseWriter, err error) {
	log.Printf("storage error: %v", err)
	fail(w, 500, "INTERNAL_ERROR", "保存或读取数据失败，请查看服务日志", "")
}

func New(store *storage.Store, origins []string) http.Handler {
	a := &API{store: store}
	mux := http.NewServeMux()
	mux.HandleFunc("GET /api/health", a.health)
	mux.HandleFunc("GET /api/issues", a.list)
	mux.HandleFunc("POST /api/issues", a.create)
	mux.HandleFunc("PATCH /api/issues/{id}/status", a.update)
	mux.HandleFunc("GET /{$}", func(w http.ResponseWriter, r *http.Request) {
		body, _ := pages.ReadFile("tester.html")
		w.Header().Set("Content-Type", "text/html; charset=utf-8")
		w.Write(body)
	})
	allowed := map[string]bool{}
	for _, o := range origins {
		allowed[strings.TrimSpace(o)] = true
	}
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("X-Content-Type-Options", "nosniff")
		origin := r.Header.Get("Origin")
		w.Header().Add("Vary", "Origin")
		if origin != "" {
			if !allowed[origin] {
				fail(w, 403, "ORIGIN_NOT_ALLOWED", "不允许的浏览器来源", "")
				return
			}
			w.Header().Set("Access-Control-Allow-Origin", origin)
			w.Header().Set("Access-Control-Allow-Methods", "GET, POST, PATCH, OPTIONS")
			w.Header().Set("Access-Control-Allow-Headers", "Content-Type")
		}
		if r.Method == http.MethodOptions {
			w.WriteHeader(http.StatusNoContent)
			return
		}
		mux.ServeHTTP(w, r)
	})
}

func decode(w http.ResponseWriter, r *http.Request, dst any) bool {
	media, _, err := mime.ParseMediaType(r.Header.Get("Content-Type"))
	if err != nil || media != "application/json" {
		fail(w, 415, "UNSUPPORTED_MEDIA_TYPE", "请使用 application/json", "")
		return false
	}
	r.Body = http.MaxBytesReader(w, r.Body, 64*1024)
	d := json.NewDecoder(r.Body)
	d.DisallowUnknownFields()
	if err = d.Decode(dst); err == nil {
		var extra any
		if next := d.Decode(&extra); next != io.EOF {
			err = errors.New("请求必须仅包含一个 JSON 对象")
		}
	}
	if err != nil {
		var large *http.MaxBytesError
		if errors.As(err, &large) {
			fail(w, 413, "BODY_TOO_LARGE", "请求内容过大", "")
		} else {
			fail(w, 400, "INVALID_JSON", "JSON 格式、字段或字段类型错误", "")
		}
		return false
	}
	return true
}

func (a *API) health(w http.ResponseWriter, r *http.Request) {
	if err := a.store.Ping(r.Context()); err != nil {
		fail(w, 503, "UNAVAILABLE", "数据库不可用", "")
		return
	}
	write(w, 200, map[string]string{"status": "ok"})
}
func (a *API) list(w http.ResponseWriter, r *http.Request) {
	items, err := a.store.List(r.Context())
	if err != nil {
		internal(w, err)
		return
	}
	write(w, 200, struct {
		Items []model.Issue `json:"items"`
	}{items})
}

func (a *API) create(w http.ResponseWriter, r *http.Request) {
	var body struct {
		Title       string `json:"title"`
		Description string `json:"description"`
		Priority    string `json:"priority"`
		Position    *struct {
			X *float64 `json:"x"`
			Y *float64 `json:"y"`
			Z *float64 `json:"z"`
		} `json:"position"`
	}
	if !decode(w, r, &body) {
		return
	}
	body.Title = strings.TrimSpace(body.Title)
	if body.Title == "" || utf8.RuneCountInString(body.Title) > 120 {
		fail(w, 400, "VALIDATION_ERROR", "标题不能为空且不能超过 120 个字符", "title")
		return
	}
	if utf8.RuneCountInString(body.Description) > 2000 {
		fail(w, 400, "VALIDATION_ERROR", "描述不能超过 2000 个字符", "description")
		return
	}
	if body.Priority != "low" && body.Priority != "medium" && body.Priority != "high" {
		fail(w, 400, "VALIDATION_ERROR", "优先级必须是 low、medium 或 high", "priority")
		return
	}
	p := body.Position
	if p == nil || p.X == nil || p.Y == nil || p.Z == nil {
		fail(w, 400, "VALIDATION_ERROR", "必须提供 position.x/y/z 数值", "position")
		return
	}
	for _, v := range []float64{*p.X, *p.Y, *p.Z} {
		if math.IsNaN(v) || math.IsInf(v, 0) {
			fail(w, 400, "VALIDATION_ERROR", "坐标必须是有限数值", "position")
			return
		}
	}
	i, err := a.store.Create(r.Context(), model.Issue{Title: body.Title, Description: body.Description, Priority: body.Priority, Position: model.Position{X: *p.X, Y: *p.Y, Z: *p.Z}})
	if err != nil {
		internal(w, err)
		return
	}
	write(w, 201, i)
}

func (a *API) update(w http.ResponseWriter, r *http.Request) {
	var body struct {
		Status string `json:"status"`
	}
	if !decode(w, r, &body) {
		return
	}
	if body.Status != "open" && body.Status != "in_progress" && body.Status != "resolved" {
		fail(w, 400, "VALIDATION_ERROR", "状态必须是 open、in_progress 或 resolved", "status")
		return
	}
	i, err := a.store.UpdateStatus(r.Context(), r.PathValue("id"), body.Status)
	if errors.Is(err, sql.ErrNoRows) {
		fail(w, 404, "NOT_FOUND", "问题不存在", "")
		return
	}
	if err != nil {
		internal(w, err)
		return
	}
	write(w, 200, i)
}
