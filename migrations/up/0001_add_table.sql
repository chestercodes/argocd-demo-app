CREATE TABLE IF NOT EXISTS todoitems (
    id uuid DEFAULT gen_random_uuid() PRIMARY KEY,
    content TEXT NOT NULL,
    is_complete BOOLEAN NOT NULL
) ;