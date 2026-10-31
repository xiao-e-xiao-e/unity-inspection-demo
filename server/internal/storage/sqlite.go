package storage

import (
	"context"
	"crypto/rand"
	"database/sql"
	"encoding/hex"
	"os"
	"path/filepath"
	"time"

	"inspection/server/internal/model"
	_ "modernc.org/sqlite"
)

type Store struct{ db *sql.DB }

func Open(path string) (*Store, error) {
	if err := os.MkdirAll(filepath.Dir(path), 0755); err != nil {
		return nil, err
	}
	db, err := sql.Open("sqlite", path)
	if err != nil {
		return nil, err
	}
	db.SetMaxOpenConns(1)
	_, err = db.Exec(`PRAGMA busy_timeout=5000;
	CREATE TABLE IF NOT EXISTS issues (
		id TEXT PRIMARY KEY, title TEXT NOT NULL, description TEXT NOT NULL,
		priority TEXT NOT NULL CHECK(priority IN ('low','medium','high')),
		status TEXT NOT NULL CHECK(status IN ('open','in_progress','resolved')),
		position_x REAL NOT NULL, position_y REAL NOT NULL, position_z REAL NOT NULL,
		created_at TEXT NOT NULL, updated_at TEXT NOT NULL
	);`)
	if err != nil {
		db.Close()
		return nil, err
	}
	return &Store{db: db}, nil
}

func (s *Store) Close() error                   { return s.db.Close() }
func (s *Store) Ping(ctx context.Context) error { return s.db.PingContext(ctx) }

const columns = `id,title,description,priority,status,position_x,position_y,position_z,created_at,updated_at`

func scan(row interface{ Scan(...any) error }) (model.Issue, error) {
	var i model.Issue
	err := row.Scan(&i.ID, &i.Title, &i.Description, &i.Priority, &i.Status, &i.Position.X, &i.Position.Y, &i.Position.Z, &i.CreatedAt, &i.UpdatedAt)
	return i, err
}

func (s *Store) Create(ctx context.Context, i model.Issue) (model.Issue, error) {
	var id [16]byte
	if _, err := rand.Read(id[:]); err != nil {
		return i, err
	}
	i.ID = hex.EncodeToString(id[:])
	i.Status = "open"
	i.CreatedAt = time.Now().UTC().Format(time.RFC3339Nano)
	i.UpdatedAt = i.CreatedAt
	_, err := s.db.ExecContext(ctx, `INSERT INTO issues (`+columns+`) VALUES (?,?,?,?,?,?,?,?,?,?)`, i.ID, i.Title, i.Description, i.Priority, i.Status, i.Position.X, i.Position.Y, i.Position.Z, i.CreatedAt, i.UpdatedAt)
	return i, err
}

func (s *Store) List(ctx context.Context) ([]model.Issue, error) {
	rows, err := s.db.QueryContext(ctx, `SELECT `+columns+` FROM issues ORDER BY created_at DESC,id DESC`)
	if err != nil {
		return nil, err
	}
	defer rows.Close()
	items := []model.Issue{}
	for rows.Next() {
		i, err := scan(rows)
		if err != nil {
			return nil, err
		}
		items = append(items, i)
	}
	return items, rows.Err()
}

func (s *Store) UpdateStatus(ctx context.Context, id, status string) (model.Issue, error) {
	return scan(s.db.QueryRowContext(ctx, `UPDATE issues SET status=?,updated_at=? WHERE id=? RETURNING `+columns, status, time.Now().UTC().Format(time.RFC3339Nano), id))
}
