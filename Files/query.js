function GetQuery(q) {
    try {
        let query = new URLSearchParams(window.location.search);
        if (query.has(q)) {
            return query.get(q);
        } else {
            return "null";
        }
    } catch {
        return "null";
    }
}