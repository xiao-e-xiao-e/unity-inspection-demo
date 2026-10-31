package main

import (
	"context"
	"errors"
	"flag"
	"inspection/server/internal/httpapi"
	"inspection/server/internal/storage"
	"log"
	"net/http"
	"os"
	"os/signal"
	"path/filepath"
	"strings"
	"time"
)

func main() {
	addr := flag.String("addr", "127.0.0.1:8080", "HTTP listen address")
	dbPath := flag.String("db", "data/inspection.db", "SQLite file path")
	origins := flag.String("origins", "http://localhost:5173,http://127.0.0.1:5173,http://localhost:8080,http://127.0.0.1:8080", "Allowed browser origins, comma separated")
	flag.Parse()
	abs, err := filepath.Abs(*dbPath)
	if err != nil {
		log.Fatal(err)
	}
	store, err := storage.Open(abs)
	if err != nil {
		log.Fatal(err)
	}
	defer store.Close()
	srv := &http.Server{Addr: *addr, Handler: httpapi.New(store, strings.Split(*origins, ",")), ReadHeaderTimeout: 5 * time.Second, ReadTimeout: 10 * time.Second, WriteTimeout: 15 * time.Second, IdleTimeout: 60 * time.Second}
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt)
	defer stop()
	shutdownDone := make(chan struct{})
	go func() {
		defer close(shutdownDone)
		<-ctx.Done()
		shutdown, cancel := context.WithTimeout(context.Background(), 5*time.Second)
		defer cancel()
		if err := srv.Shutdown(shutdown); err != nil {
			log.Printf("shutdown: %v", err)
		}
	}()
	log.Printf("SQLite: %s", abs)
	log.Printf("API and manual tester: http://%s", *addr)
	if err := srv.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
		log.Fatal(err)
	}
	<-shutdownDone
}
