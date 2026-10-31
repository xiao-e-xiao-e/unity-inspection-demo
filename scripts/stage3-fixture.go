// Test-only error/timeout endpoints. This is never used by the real application.
package main

import (
	"log"
	"net/http"
	"time"
)

func main() {
	mux := http.NewServeMux()
	mux.HandleFunc("/malformed/api/issues", func(w http.ResponseWriter, r *http.Request) { w.WriteHeader(201); w.Write([]byte("not json")) })
	mux.HandleFunc("/error500/api/issues", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(500)
		w.Write([]byte(`{"error":{"code":"INTERNAL_ERROR","message":"测试服务错误"}}`))
	})
	mux.HandleFunc("/slow/api/issues", func(w http.ResponseWriter, r *http.Request) {
		select {
		case <-time.After(10 * time.Second):
			w.WriteHeader(201)
		case <-r.Context().Done():
			return
		}
	})
	log.Fatal(http.ListenAndServe("127.0.0.1:18084", mux))
}
